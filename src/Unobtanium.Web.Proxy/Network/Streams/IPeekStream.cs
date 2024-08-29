using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.StreamExtended.Network;

/// <summary>
/// The IPeekStream interface defines the contract for a stream that supports peeking at its data without consuming it.
/// It provides methods for peeking at a single byte or multiple bytes, either synchronously or asynchronously.
/// This interface is designed to be used in the context of network streams, where it is often necessary to look ahead in the data without consuming it.
/// </summary>
public interface IPeekStream
{
    /// <summary>
    /// Peeks at a single byte from the buffer at the specified index.
    /// </summary>
    /// <param name="index">The index in the buffer to peek at.</param>
    /// <returns>The byte at the specified index in the buffer.</returns>
    /// <exception cref="Exception">Thrown when the index is out of the buffer size.</exception>
    byte PeekByteFromBuffer ( int index );

    /// <summary>
    /// Asynchronously peeks at a single byte from the buffer at the specified index.
    /// </summary>
    /// <param name="index">The index in the buffer to peek at.</param>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A ValueTask that completes when the byte has been peeked, yielding the byte as an integer.</returns>
    ValueTask<int> PeekByteAsync ( int index, CancellationToken cancellationToken = default );

    /// <summary>
    /// Asynchronously peeks at multiple bytes from the buffer, copying them into the provided buffer.
    /// </summary>
    /// <param name="buffer">The buffer to copy the bytes into.</param>
    /// <param name="offset">The offset in the destination buffer at which to start copying.</param>
    /// <param name="index">The index in the source buffer at which to start peeking.</param>
    /// <param name="count">The number of bytes to peek.</param>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A ValueTask that completes when the bytes have been peeked, yielding the number of bytes peeked as an integer.</returns>
    ValueTask<int> PeekBytesAsync ( byte[] buffer, int offset, int index, int count,
        CancellationToken cancellationToken = default );
}
