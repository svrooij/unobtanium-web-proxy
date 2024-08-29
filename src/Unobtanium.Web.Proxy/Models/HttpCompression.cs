namespace Titanium.Web.Proxy.Compression;

/// <summary>
///     Supported http compression types
/// </summary>
public enum HttpCompression
{
    /// <summary>Unsupported compression</summary>
    Unsupported,
    /// <summary>Gzip compression</summary>
    Gzip,
    /// <summary>Deflate compression</summary>
    Deflate,
    /// <summary>Brotli compression</summary>
    Brotli
}
