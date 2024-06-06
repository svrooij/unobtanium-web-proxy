namespace Titanium.Web.Proxy.Http;

/// <summary>
/// The TunnelType enumeration represents the type of tunnel used in a proxy connection.
/// </summary>
public enum TunnelType
{
    /// <summary>
    /// The tunnel type is unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// The tunnel is used for HTTPS connections.
    /// </summary>
    Https,

    /// <summary>
    /// The tunnel is used for WebSocket connections.
    /// </summary>
    Websocket,

    /// <summary>
    /// The tunnel is used for HTTP/2 connections.
    /// </summary>
    Http2
}
