using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Exceptions;
using Unobtanium.Web.Proxy.StreamExtended.BufferPool;
using Unobtanium.Web.Proxy.StreamExtended.Network;

namespace Unobtanium.Web.Proxy.EventArguments;

internal class LimitedStream : Stream
{
    private readonly IHttpStreamReader baseReader;
    private readonly IBufferPool bufferPool;
    private readonly bool isChunked;
    private long bytesRemaining;

    private bool readChunkTrail;

    internal LimitedStream ( IHttpStreamReader baseStream, IBufferPool bufferPool, bool isChunked,
        long contentLength )
    {
        baseReader = baseStream;
        this.bufferPool = bufferPool;
        this.isChunked = isChunked;
        bytesRemaining = isChunked
            ? 0
            : contentLength == -1
                ? long.MaxValue
                : contentLength;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    //private void GetNextChunk()
    //{
    //    if (readChunkTrail)
    //    {
    //        // read the chunk trail of the previous chunk
    //        var s = baseReader.ReadLineAsync().Result;
    //        if (s == null)
    //        {
    //            bytesRemaining = -1;
    //            return;
    //        }
    //    }

    //    readChunkTrail = true;

    //    var chunkHead = baseReader.ReadLineAsync().Result;
    //    if (chunkHead == null)
    //    {
    //        bytesRemaining = -1;
    //        return;
    //    }

    //    var idx = chunkHead.IndexOf(';');
    //    if (idx >= 0) chunkHead = chunkHead[..idx];

    //    if (!int.TryParse(chunkHead, NumberStyles.HexNumber, null, out var chunkSize))
    //        throw new ProxyHttpException($"Invalid chunk length: '{chunkHead}'", null, null);

    //    bytesRemaining = chunkSize;

    //    if (chunkSize == 0)
    //    {
    //        bytesRemaining = -1;

    //        // chunk trail
    //        var task = baseReader.ReadLineAsync();
    //        if (!task.IsCompleted)
    //            task.AsTask().Wait();
    //    }
    //}

    private async Task GetNextChunkAsync ( CancellationToken cancellationToken )
    {
        if (readChunkTrail)
        {
            // read the chunk trail of the previous chunk
            var s = await baseReader.ReadLineAsync(cancellationToken);
            if (s == null)
            {
                bytesRemaining = -1;
                return;
            }
        }

        readChunkTrail = true;

        var chunkHead = await baseReader.ReadLineAsync(cancellationToken);
        if (chunkHead == null)
        {
            bytesRemaining = -1;
            return;
        }

        var idx = chunkHead.IndexOf(';');
        if (idx >= 0) chunkHead = chunkHead[..idx];

        if (!int.TryParse(chunkHead, NumberStyles.HexNumber, null, out var chunkSize))
            throw new ProxyHttpException($"Invalid chunk length: '{chunkHead}'", null, null);

        bytesRemaining = chunkSize;

        if (chunkSize == 0)
        {
            bytesRemaining = -1;

            // chunk trail
            await baseReader.ReadLineAsync(cancellationToken);
        }
    }

    public override void Flush ()
    {
        throw new NotSupportedException();
    }

    public override long Seek ( long offset, SeekOrigin origin )
    {
        throw new NotSupportedException();
    }

    public override void SetLength ( long value )
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Reads a sequence of bytes from the stream. Not supported. Use ReadAsync instead.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override int Read ( byte[] buffer, int offset, int count )
    {
        throw new NotImplementedException();
    }

    public override async Task<int> ReadAsync ( byte[] buffer, int offset, int count, CancellationToken cancellationToken )
    {
        if (bytesRemaining == -1) return 0;

        if (bytesRemaining == 0)
        {
            if (isChunked)
                await GetNextChunkAsync(cancellationToken);
            else
                bytesRemaining = -1;
        }

        if (bytesRemaining == -1) return 0;

        var toRead = (int)Math.Min(count, bytesRemaining);
        var res = await baseReader.ReadAsync(buffer, offset, toRead, cancellationToken);
        bytesRemaining -= res;

        if (res == 0) bytesRemaining = -1;

        return res;
    }

    public async Task Finish ( CancellationToken cancellationToken )
    {
        if (bytesRemaining != -1)
        {
            var buffer = bufferPool.GetBuffer();
            try
            {
                var res = await ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (res != 0) throw new InvalidOperationException("Data received after stream end");
            }
            finally
            {
                bufferPool.ReturnBuffer(buffer);
            }
        }
    }

    public override void Write ( byte[] buffer, int offset, int count )
    {
        throw new NotSupportedException();
    }
}
