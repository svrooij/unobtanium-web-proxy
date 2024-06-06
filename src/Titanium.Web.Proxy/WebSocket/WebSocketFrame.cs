using System;
using System.Text;

namespace Titanium.Web.Proxy;

/// <summary>
/// The WebSocketFrame class represents a single frame of data in a WebSocket connection.
/// It provides properties for accessing the finality, operation code, and data of the frame,
/// as well as methods for getting the data as a text string.
/// </summary>
public class WebSocketFrame
{
    /// <summary>
    /// Gets a value indicating whether this frame is the final frame in a message.
    /// </summary>
    public bool IsFinal { get; internal set; }

    /// <summary>
    /// Gets the operation code for this frame, which indicates the type of data contained in the frame.
    /// </summary>
    public WebsocketOpCode OpCode { get; internal set; }

    /// <summary>
    /// Gets the data contained in this frame as a read-only memory of bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; internal set; }

    /// <summary>
    /// Gets the data contained in this frame as a text string, using UTF-8 encoding.
    /// </summary>
    /// <returns>The data as a text string.</returns>
    public string GetText()
    {
        return GetText(Encoding.UTF8);
    }

    /// <summary>
    /// Gets the data contained in this frame as a text string, using the specified encoding.
    /// </summary>
    /// <param name="encoding">The encoding to use when converting the data to a string.</param>
    /// <returns>The data as a text string.</returns>
    public string GetText(Encoding encoding)
    {
#if NET6_0_OR_GREATER
        return encoding.GetString(Data.Span);
#else
        return encoding.GetString(Data.ToArray());
#endif
    }
}