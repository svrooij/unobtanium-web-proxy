using System;

namespace Titanium.Web.Proxy.StreamExtended.Network;

/// <summary>
///     Wraps the data sent/received event argument.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DataEventArgs"/> class.
/// </remarks>
/// <param name="buffer">The buffer containing the data.</param>
/// <param name="offset">The offset in the buffer where the data begins.</param>
/// <param name="count">The number of bytes of data in the buffer.</param>
public class DataEventArgs ( byte[] buffer, int offset, int count ) : EventArgs
{

    /// <summary>
    ///     The buffer with data.
    /// </summary>
    public byte[] Buffer { get; } = buffer;

    /// <summary>
    ///     Offset in buffer from which valid data begins.
    /// </summary>
    public int Offset { get; } = offset;

    /// <summary>
    ///     Length from offset in buffer with valid data.
    /// </summary>
    public int Count { get; } = count;
}
