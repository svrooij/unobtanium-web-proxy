namespace Titanium.Web.Proxy.Http;

/// <summary>
/// The KnownHeaders class provides static instances of known HTTP headers.
/// These instances can be used to avoid the overhead of creating new strings for common headers.
/// </summary>
public static class KnownHeaders
{
    /// <summary>
    /// The 'Connection' HTTP header.
    /// </summary>
    public static KnownHeader Connection = "Connection";

    /// <summary>
    /// The 'close' value for the 'Connection' HTTP header.
    /// </summary>
    public static KnownHeader ConnectionClose = "close";

    /// <summary>
    /// The 'keep-alive' value for the 'Connection' HTTP header.
    /// </summary>
    public static KnownHeader ConnectionKeepAlive = "keep-alive";

    /// <summary>
    /// The 'Content-Length' HTTP header.
    /// </summary>
    public static KnownHeader ContentLength = "Content-Length";

    /// <summary>
    /// The 'content-length' HTTP header for HTTP/2.
    /// </summary>
    public static KnownHeader ContentLengthHttp2 = "content-length";

    /// <summary>
    /// The 'Content-Type' HTTP header.
    /// </summary>
    public static KnownHeader ContentType = "Content-Type";

    /// <summary>
    /// The 'charset' value for the 'Content-Type' HTTP header.
    /// </summary>
    public static KnownHeader ContentTypeCharset = "charset";

    /// <summary>
    /// The 'boundary' value for the 'Content-Type' HTTP header.
    /// </summary>
    public static KnownHeader ContentTypeBoundary = "boundary";

    /// <summary>
    /// The 'Upgrade' HTTP header.
    /// </summary>
    public static KnownHeader Upgrade = "Upgrade";

    /// <summary>
    /// The 'websocket' value for the 'Upgrade' HTTP header.
    /// </summary>
    public static KnownHeader UpgradeWebsocket = "websocket";

    /// <summary>
    /// The 'Accept-Encoding' HTTP header.
    /// </summary>
    public static KnownHeader AcceptEncoding = "Accept-Encoding";

    /// <summary>
    /// The 'Authorization' HTTP header.
    /// </summary>
    public static KnownHeader Authorization = "Authorization";

    /// <summary>
    /// The 'Expect' HTTP header.
    /// </summary>
    public static KnownHeader Expect = "Expect";

    /// <summary>
    /// The '100-continue' expectation for the 'Expect' HTTP header.
    /// </summary>
    public static KnownHeader Expect100Continue = "100-continue";

    /// <summary>
    /// The 'Host' HTTP header.
    /// </summary>
    public static KnownHeader Host = "Host";

    /// <summary>
    /// The 'Proxy-Authorization' HTTP header.
    /// </summary>
    public static KnownHeader ProxyAuthorization = "Proxy-Authorization";

    /// <summary>
    /// The 'basic' value for the 'Proxy-Authorization' HTTP header.
    /// </summary>
    public static KnownHeader ProxyAuthorizationBasic = "basic";

    /// <summary>
    /// The 'Proxy-Connection' HTTP header.
    /// </summary>
    public static KnownHeader ProxyConnection = "Proxy-Connection";

    /// <summary>
    /// The 'close' value for the 'Proxy-Connection' HTTP header.
    /// </summary>
    public static KnownHeader ProxyConnectionClose = "close";

    /// <summary>
    /// The 'Content-Encoding' HTTP header.
    /// </summary>
    public static KnownHeader ContentEncoding = "Content-Encoding";

    /// <summary>
    /// The 'deflate' value for the 'Content-Encoding' HTTP header.
    /// </summary>
    public static KnownHeader ContentEncodingDeflate = "deflate";

    /// <summary>
    /// The 'gzip' value for the 'Content-Encoding' HTTP header.
    /// </summary>
    public static KnownHeader ContentEncodingGzip = "gzip";

    /// <summary>
    /// The 'br' (Brotli) value for the 'Content-Encoding' HTTP header.
    /// </summary>
    public static KnownHeader ContentEncodingBrotli = "br";

    /// <summary>
    /// The 'Location' HTTP header.
    /// </summary>
    public static KnownHeader Location = "Location";

    /// <summary>
    /// The 'Proxy-Authenticate' HTTP header.
    /// </summary>
    public static KnownHeader ProxyAuthenticate = "Proxy-Authenticate";

    /// <summary>
    /// The 'Transfer-Encoding' HTTP header.
    /// </summary>
    public static KnownHeader TransferEncoding = "Transfer-Encoding";

    /// <summary>
    /// The 'chunked' value for the 'Transfer-Encoding' HTTP header.
    /// </summary>
    public static KnownHeader TransferEncodingChunked = "chunked";
}
