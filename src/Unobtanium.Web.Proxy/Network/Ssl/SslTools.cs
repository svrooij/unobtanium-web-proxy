using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.StreamExtended.BufferPool;
using Unobtanium.Web.Proxy.StreamExtended.Models;
using Unobtanium.Web.Proxy.StreamExtended.Network;

namespace Unobtanium.Web.Proxy.StreamExtended;

/// <summary>
///     Use this class to peek SSL client/server hello information.
/// </summary>
internal class SslTools
{
    private static int ReadInt24BigEndian(ReadOnlySpan<byte> span)
    {
        if (span.Length < 3)
            throw new ArgumentException("Span must be at least 3 bytes long to read a 24-bit integer.", nameof(span));

        return (span[0] << 16) | (span[1] << 8) | span[2];
    }

    private static byte[] ReadBytesFromSequence(ReadOnlySequence<byte> sequence, int offset, int length)
    {
        var result = new byte[length];
        var position = sequence.GetPosition(offset);
        sequence.Slice(position, length).CopyTo(result);
        return result;
    }

    // Wrapper to adapt IPeekStream to Stream
    private class PeekStreamWrapper : Stream
    {
        private readonly IPeekStream _peekStream;

        public PeekStreamWrapper(IPeekStream peekStream)
        {
            _peekStream = peekStream;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Synchronous reads are not supported.");
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _peekStream.PeekBytesAsync(buffer, offset, 0, count, cancellationToken).AsTask();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    ///     Peek the SSL client hello information.
    /// </summary>
    /// <param name="clientStream"></param>
    /// <param name="bufferPool"></param>
    /// <param name="activityContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ClientHelloInfo?> PeekClientHello(IPeekStream clientStream, IBufferPool bufferPool, ActivityContext? activityContext,
        CancellationToken cancellationToken = default)
    {
        using var activity = ProxyServer.ProxyActivitySource.StartActivity("PeekClientHello", ActivityKind.Internal, activityContext ?? default);
        // Wrap IPeekStream in a Stream
        var streamWrapper = new PeekStreamWrapper(clientStream);
        var pipeReader = PipeReader.Create(streamWrapper);

        try
        {
            while (true)
            {
                // Read data from the PipeReader
                var result = await pipeReader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                try
                {
                    // Check if we have enough data to determine the record type
                    if (buffer.Length < 1)
                    {
                        if (result.IsCompleted) return null;
                        continue;
                    }

                    var recordType = buffer.First.Span[0];

                    if ((recordType & 0x80) == 0x80)
                    {
                        // SSL 2
                        if (buffer.Length < 10)
                        {
                            if (result.IsCompleted) return null;
                            continue;
                        }

                        var recordLength = ((recordType & 0x7f) << 8) + buffer.Slice(1, 1).First.Span[0];
                        if (recordLength < 9) return null;

                        if (buffer.Slice(2, 1).First.Span[0] != 0x01) return null; // Should be ClientHello

                        var majorVersion = buffer.Slice(3, 1).First.Span[0];
                        var minorVersion = buffer.Slice(4, 1).First.Span[0];

                        var ciphersCount = BinaryPrimitives.ReadInt16BigEndian(buffer.Slice(5, 2).First.Span) / 3;
                        var sessionIdLength = BinaryPrimitives.ReadInt16BigEndian(buffer.Slice(7, 2).First.Span);
                        var randomLength = BinaryPrimitives.ReadInt16BigEndian(buffer.Slice(9, 2).First.Span);

                        var requiredLength = 10 + ciphersCount * 3 + sessionIdLength + randomLength;
                        if (buffer.Length < requiredLength)
                        {
                            if (result.IsCompleted) return null;
                            continue;
                        }

                        var ciphers = new int[ciphersCount];
                        var ciphersSpan = buffer.Slice(10, ciphersCount * 3).First.Span;
                        for (int i = 0; i < ciphersCount; i++)
                        {
                            ciphers[i] = (ciphersSpan[i * 3] << 16) + (ciphersSpan[i * 3 + 1] << 8) + ciphersSpan[i * 3 + 2];
                        }

                        var sessionId = ReadBytesFromSequence(buffer, 10 + ciphersCount * 3, sessionIdLength);
                        var random = ReadBytesFromSequence(buffer, 10 + ciphersCount * 3 + sessionIdLength, randomLength);

                        return new ClientHelloInfo(2, majorVersion, minorVersion, random, sessionId, ciphers, requiredLength);
                    }

                    if (recordType == 0x16)
                    {
                        // SSL 3.0 or TLS 1.0, 1.1, 1.2
                        if (buffer.Length < 43)
                        {
                            if (result.IsCompleted) return null;
                            continue;
                        }

                        var majorVersion = buffer.Slice(1, 1).First.Span[0];
                        var minorVersion = buffer.Slice(2, 1).First.Span[0];

                        var recordLength = BinaryPrimitives.ReadInt16BigEndian(buffer.Slice(3, 2).First.Span);
                        if (buffer.Slice(5, 1).First.Span[0] != 0x01) return null; // Should be ClientHello

                        var length = ReadInt24BigEndian(buffer.Slice(6, 3).First.Span);

                        majorVersion = buffer.Slice(9, 1).First.Span[0];
                        minorVersion = buffer.Slice(10, 1).First.Span[0];

                        var random = ReadBytesFromSequence(buffer, 11, 32);
                        var sessionIdLength = buffer.Slice(43, 1).First.Span[0];

                        var requiredLength = 44 + sessionIdLength + 2; // Minimum length with sessionId and ciphersData length
                        if (buffer.Length < requiredLength)
                        {
                            if (result.IsCompleted) return null;
                            continue;
                        }

                        var sessionId = ReadBytesFromSequence(buffer, 44, sessionIdLength);

                        var ciphersLength = BinaryPrimitives.ReadInt16BigEndian(buffer.Slice(44 + sessionIdLength, 2).First.Span);
                        requiredLength += ciphersLength + 1; // Add ciphersData length and compressionData length
                        if (buffer.Length < requiredLength)
                        {
                            if (result.IsCompleted) return null;
                            continue;
                        }

                        var ciphers = new int[ciphersLength / 2];
                        var ciphersSpan = buffer.Slice(44 + sessionIdLength + 2, ciphersLength).First.Span;
                        for (int i = 0; i < ciphers.Length; i++)
                        {
                            ciphers[i] = BinaryPrimitives.ReadInt16BigEndian(ciphersSpan.Slice(i * 2, 2));
                        }

                        var compressionLength = buffer.Slice(44 + sessionIdLength + 2 + ciphersLength, 1).First.Span[0];
                        requiredLength += compressionLength;
                        if (buffer.Length < requiredLength)
                        {
                            if (result.IsCompleted) return null;
                            continue;
                        }

                        var compressionData = ReadBytesFromSequence(buffer, 44 + sessionIdLength + 2 + ciphersLength + 1, compressionLength);

                        var extensionsStartPosition = 44 + sessionIdLength + 2 + ciphersLength + 1 + compressionLength;

                        Dictionary<string, SslExtension>? extensions = null;
                        if (extensionsStartPosition < recordLength + 5)
                        {
                            extensions = await ReadExtensions(majorVersion, minorVersion,
                                new PeekStreamReader(clientStream, extensionsStartPosition), cancellationToken);
                        }

                        return new ClientHelloInfo(3, majorVersion, minorVersion, random, sessionId, ciphers, requiredLength)
                        {
                            ExtensionsStartPosition = extensionsStartPosition,
                            CompressionData = compressionData,
                            Extensions = extensions
                        };
                    }

                    return null;
                }
                finally
                {
                    // Mark the consumed and examined portions of the buffer
                    pipeReader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }
        finally
        {
            await pipeReader.CompleteAsync();
        }
    }

    /// <summary>
    ///     Is the given stream starts with an SSL client hello?
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="bufferPool"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<bool> IsServerHello(IPeekStream stream, IBufferPool bufferPool,
        CancellationToken cancellationToken)
    {
        var serverHello = await PeekServerHello(stream, bufferPool, cancellationToken);
        return serverHello != null;
    }

    /// <summary>
    ///     Peek the SSL client hello information.
    /// </summary>
    /// <param name="serverStream"></param>
    /// <param name="bufferPool"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ServerHelloInfo?> PeekServerHello(IPeekStream serverStream, IBufferPool bufferPool,
        CancellationToken cancellationToken = default)
    {
        // detects the HTTPS ClientHello message as it is described in the following url:
        // https://stackoverflow.com/questions/3897883/how-to-detect-an-incoming-ssl-https-handshake-ssl-wire-format

        var recordType = await serverStream.PeekByteAsync(0, cancellationToken);
        if (recordType == -1) return null;

        if ((recordType & 0x80) == 0x80)
        {
            // SSL 2
            // not tested. SSL2 is deprecated
            var peekStream = new PeekStreamReader(serverStream, 1);

            // length value + minimum length
            if (!await peekStream.EnsureBufferLength(39, cancellationToken)) return null;

            var recordLength = ((recordType & 0x7f) << 8) + peekStream.ReadByte();
            if (recordLength < 38)
                // Message body too short.
                return null;

            if (peekStream.ReadByte() != 0x04)
                // should be ServerHello
                return null;

            int majorVersion = peekStream.ReadByte();
            int minorVersion = peekStream.ReadByte();

            // 32 bytes random + 1 byte sessionId + 2 bytes cipherSuite
            if (!await peekStream.EnsureBufferLength(35, cancellationToken)) return null;

            var random = peekStream.ReadBytes(32);
            var sessionId = peekStream.ReadBytes(1);
            var cipherSuite = peekStream.ReadInt16();

            var serverHelloInfo = new ServerHelloInfo(2, majorVersion, minorVersion, random, sessionId, cipherSuite,
                peekStream.Position);

            return serverHelloInfo;
        }

        if (recordType == 0x16)
        {
            var peekStream = new PeekStreamReader(serverStream, 1);

            // should contain at least 43 bytes
            // 2 version + 2 length + 1 type + 3 length(?) + 2 version +  32 random + 1 sessionid length
            if (!await peekStream.EnsureBufferLength(43, cancellationToken)) return null;

            // SSL 3.0 or TLS 1.0, 1.1 and 1.2
            int majorVersion = peekStream.ReadByte();
            int minorVersion = peekStream.ReadByte();

            var recordLength = peekStream.ReadInt16();

            if (peekStream.ReadByte() != 0x02)
                // should be ServerHello
                return null;

            var length = peekStream.ReadInt24();

            majorVersion = peekStream.ReadByte();
            minorVersion = peekStream.ReadByte();

            var random = peekStream.ReadBytes(32);
            length = peekStream.ReadByte();

            // sessionid + cipherSuite + compressionMethod
            if (!await peekStream.EnsureBufferLength(length + 2 + 1, cancellationToken)) return null;

            var sessionId = peekStream.ReadBytes(length);

            var cipherSuite = peekStream.ReadInt16();
            var compressionMethod = peekStream.ReadByte();

            var extensionsStartPosition = peekStream.Position;

            Dictionary<string, SslExtension>? extensions = null;

            if (extensionsStartPosition < recordLength + 5)
                extensions = await ReadExtensions(majorVersion, minorVersion, peekStream, cancellationToken);

            var serverHelloInfo = new ServerHelloInfo(3, majorVersion, minorVersion, random, sessionId, cipherSuite,
                peekStream.Position)
            {
                CompressionMethod = compressionMethod,
                ExtensionsStartPosition = extensionsStartPosition,
                Extensions = extensions
            };

            return serverHelloInfo;
        }

        return null;
    }

    private static async Task<Dictionary<string, SslExtension>?> ReadExtensions(int majorVersion, int minorVersion,
        PeekStreamReader peekStreamReader, CancellationToken cancellationToken)
    {
        Dictionary<string, SslExtension>? extensions = null;
        if (majorVersion > 3 || majorVersion == 3 && minorVersion >= 1)
            if (await peekStreamReader.EnsureBufferLength(2, cancellationToken))
            {
                var extensionsLength = peekStreamReader.ReadInt16();

                if (await peekStreamReader.EnsureBufferLength(extensionsLength, cancellationToken))
                {
                    var extensionsData = peekStreamReader.ReadBytes(extensionsLength).AsMemory();
                    extensions = new Dictionary<string, SslExtension>();
                    var idx = 0;
                    while (extensionsData.Length > 3)
                    {
                        var id = BinaryPrimitives.ReadInt16BigEndian(extensionsData.Span);
                        var length = BinaryPrimitives.ReadInt16BigEndian(extensionsData.Span.Slice(2));
                        var extension = new SslExtension(id, extensionsData.Slice(4, length), idx++);
                        extensions[extension.Name] = extension;
                        extensionsData = extensionsData.Slice(4 + length);
                    }
                }
            }

        return extensions;
    }
}
