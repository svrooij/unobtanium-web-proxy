using System;

namespace Titanium.Web.Proxy.StreamExtended.BufferPool;

/// <summary>
/// The IBufferPool interface defines the contract for a custom buffer pool.
/// To use the default buffer pool implementation, use the DefaultBufferPool class.
/// </summary>
public interface IBufferPool : IDisposable
{
    /// <summary>
    /// Gets the size of the buffer.
    /// </summary>
    int BufferSize { get; }

    /// <summary>
    /// Retrieves a buffer from the pool.
    /// </summary>
    /// <returns>A byte array representing the buffer.</returns>
    byte[] GetBuffer ();

    /// <summary>
    /// Retrieves a buffer of a specific size from the pool.
    /// </summary>
    /// <param name="bufferSize">The size of the buffer to retrieve.</param>
    /// <returns>A byte array representing the buffer.</returns>
    byte[] GetBuffer ( int bufferSize );

    /// <summary>
    /// Returns a buffer to the pool.
    /// </summary>
    /// <param name="buffer">The buffer to return to the pool.</param>
    void ReturnBuffer ( byte[] buffer );
}
