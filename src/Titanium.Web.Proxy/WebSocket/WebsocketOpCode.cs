namespace Titanium.Web.Proxy;

/// <summary>
/// The WebsocketOpCode enumeration represents the operation codes for WebSocket frames.
/// These codes are used to indicate the type of data contained in a WebSocket message frame.
/// </summary>
public enum WebsocketOpCode : byte
{
    /// <summary>
    /// Indicates a continuation frame.
    /// </summary>
    Continuation,

    /// <summary>
    /// Indicates a text frame.
    /// </summary>
    Text,

    /// <summary>
    /// Indicates a binary frame.
    /// </summary>
    Binary,

    /// <summary>
    /// Indicates a connection close frame.
    /// </summary>
    ConnectionClose = 8,

    /// <summary>
    /// Indicates a ping frame.
    /// </summary>
    Ping,

    /// <summary>
    /// Indicates a pong frame.
    /// </summary>
    Pong
}