using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unobtanium.Web.Proxy.Extensions;
using Unobtanium.Web.Proxy.StreamExtended.Models;

namespace Unobtanium.Web.Proxy.StreamExtended;

/// <summary>
///     Wraps up the server SSL hello information.
/// </summary>
public class ServerHelloInfo
{
    private static readonly string[] compressions =
    {
        "null",
        "DEFLATE"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerHelloInfo"/> class.
    /// </summary>
    /// <param name="handshakeVersion">The handshake version.</param>
    /// <param name="majorVersion">The major version.</param>
    /// <param name="minorVersion">The minor version.</param>
    /// <param name="random">The random bytes.</param>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cipherSuite">The cipher suite.</param>
    /// <param name="serverHelloLength">The length of the server hello message.</param>
    public ServerHelloInfo ( int handshakeVersion, int majorVersion, int minorVersion, byte[] random,
        byte[] sessionId, int cipherSuite, int serverHelloLength )
    {
        HandshakeVersion = handshakeVersion;
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
        Random = random;
        SessionId = sessionId;
        CipherSuite = cipherSuite;
        ServerHelloLength = serverHelloLength;
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
    /// Gets the cipher suite.
    /// </summary>
    public int CipherSuite { get; }

    /// <summary>
    /// Gets or sets the compression method.
    /// </summary>
    public byte CompressionMethod { get; set; }

    /// <summary>
    /// Gets the length of the server hello message.
    /// </summary>
    internal int ServerHelloLength { get; }

    /// <summary>
    /// Gets or sets the start position of the extensions in the server hello message.
    /// </summary>
    internal int ExtensionsStartPosition { get; set; }

    /// <summary>
    /// Gets or sets the extensions in the server hello message.
    /// </summary>
    public Dictionary<string, SslExtension>? Extensions { get; set; }

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
            $"A SSLv{HandshakeVersion}-compatible ServerHello handshake was found. Titanium extracted the parameters below.");
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

        var compression = compressions.Length > CompressionMethod
            ? compressions[CompressionMethod]
            : $"unknown [0x{CompressionMethod:X2}]";
        sb.AppendLine($"Compression: {compression}");

        sb.Append("Cipher:");
        if (!SslCiphers.Ciphers.TryGetValue(CipherSuite, out var cipherStr)) cipherStr = "unknown";

        sb.AppendLine($"[0x{CipherSuite:X4}] {cipherStr}");

        return sb.ToString();
    }
}
