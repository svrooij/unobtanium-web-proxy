using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using Titanium.Web.Proxy.Extensions;
using Titanium.Web.Proxy.StreamExtended.Models;

namespace Titanium.Web.Proxy.StreamExtended;

/// <summary>
/// Represents the client SSL hello information.
/// </summary>
public class ClientHelloInfo
{
    private static readonly string[] compressions =
    {
        "null",
        "DEFLATE"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientHelloInfo"/> class.
    /// </summary>
    /// <param name="handshakeVersion">The handshake version.</param>
    /// <param name="majorVersion">The major version.</param>
    /// <param name="minorVersion">The minor version.</param>
    /// <param name="random">The random bytes.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="ciphers">The ciphers.</param>
    /// <param name="clientHelloLength">The length of the client hello message.</param>
    internal ClientHelloInfo ( int handshakeVersion, int majorVersion, int minorVersion, byte[] random, byte[] sessionId,
        int[] ciphers, int clientHelloLength )
    {
        HandshakeVersion = handshakeVersion;
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
        Random = random;
        SessionId = sessionId;
        Ciphers = ciphers;
        ClientHelloLength = clientHelloLength;
    }

    /// <summary>
    /// Gets the handshake version.
    /// </summary>
    public int HandshakeVersion { get; }

    /// <summary>
    /// Gets the major version.
    /// </summary>
    public int MajorVersion { get; }

    /// <summary>
    /// Gets the minor version.
    /// </summary>
    public int MinorVersion { get; }

    /// <summary>
    /// Gets the random bytes.
    /// </summary>
    public byte[] Random { get; }

    /// <summary>
    /// Gets the time derived from the random bytes.
    /// </summary>
    public DateTime Time
    {
        get
        {
            var time = DateTime.MinValue;
            if (Random.Length > 3)
                time = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(((uint)Random[3] << 24) + ((uint)Random[2] << 16) + ((uint)Random[1] << 8) + Random[0])
                    .ToLocalTime();

            return time;
        }
    }

    /// <summary>
    /// Gets the session ID.
    /// </summary>
    public byte[] SessionId { get; }

    /// <summary>
    /// Gets the ciphers.
    /// </summary>
    public int[] Ciphers { get; }

    /// <summary>
    /// Gets or sets the compression data.
    /// </summary>
    public byte[]? CompressionData { get; internal set; }

    /// <summary>
    /// Gets the length of the client hello message.
    /// </summary>
    internal int ClientHelloLength { get; }

    /// <summary>
    /// Gets or sets the start position of the extensions in the client hello message.
    /// </summary>
    internal int ExtensionsStartPosition { get; set; }

    /// <summary>
    /// Gets or sets the extensions in the client hello message.
    /// </summary>
    public Dictionary<string, SslExtension>? Extensions { get; set; }

    /// <summary>
    /// Gets the SSL protocol used in the client hello message.
    /// </summary>
    public SslProtocols SslProtocol
    {
        get
        {
            var major = MajorVersion;
            var minor = MinorVersion;
            if (major == 3 && minor == 3)
            {

                var protocols = this.GetSslProtocols();
                if (protocols != null)
                {
                    if (protocols.Contains("Tls1.3"))
                    {
                        return SslProtocols.Tls12 | SslProtocols.Tls13;
                    }
                }

                return SslProtocols.Tls12;
            }
#pragma warning disable 618
            if (major == 3 && minor == 2)
                return SslProtocols.Tls11;

            if (major == 3 && minor == 1)
                return SslProtocols.Tls;


            if (major == 3 && minor == 0)
                return SslProtocols.Ssl3;

            if (major == 2 && minor == 0)
                return SslProtocols.Ssl2;
#pragma warning restore 618

            return SslProtocols.None;
        }
    }

    private static string SslVersionToString ( int major, int minor )
    {
        var str = "Unknown";
        if (major == 3 && minor == 3)
            str = "TLS/1.2";
        else if (major == 3 && minor == 2)
            str = "TLS/1.1";
        else if (major == 3 && minor == 1)
            str = "TLS/1.0";
        else if (major == 3 && minor == 0)
            str = "SSL/3.0";
        else if (major == 2 && minor == 0)
            str = "SSL/2.0";

        return $"{major}.{minor} ({str})";
    }

    /// <summary>
    ///     Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>
    ///     A <see cref="System.String" /> that represents this instance.
    /// </returns>
    public override string ToString ()
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"A SSLv{HandshakeVersion}-compatible ClientHello handshake was found. Titanium extracted the parameters below.");
        sb.AppendLine();
        sb.AppendLine($"Version: {SslVersionToString(MajorVersion, MinorVersion)}");
        sb.AppendLine($"Random: {StringExtensions.ByteArrayToHexString(Random)}");
        sb.AppendLine($"\"Time\": {Time}");
        sb.AppendLine($"SessionID: {StringExtensions.ByteArrayToHexString(SessionId)}");

        if (Extensions != null)
        {
            sb.AppendLine("Extensions:");
            foreach (var extension in Extensions.Values.OrderBy(x => x.Position))
                sb.AppendLine($"{extension.Name}: {extension.Data}");
        }

        if (CompressionData != null && CompressionData.Length > 0)
        {
            int compressionMethod = CompressionData[0];
            var compression = compressions.Length > compressionMethod
                ? compressions[compressionMethod]
                : $"unknown [0x{compressionMethod:X2}]";
            sb.AppendLine($"Compression: {compression}");
        }

        if (Ciphers.Length > 0)
        {
            sb.AppendLine("Ciphers:");
            foreach (var cipherSuite in Ciphers)
            {
                if (!SslCiphers.Ciphers.TryGetValue(cipherSuite, out var cipherStr)) cipherStr = "unknown";

                sb.AppendLine($"[0x{cipherSuite:X4}] {cipherStr}");
            }
        }

        return sb.ToString();
    }
}
