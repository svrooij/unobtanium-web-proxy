using System.Threading;
using System.Threading.Tasks;

namespace Titanium.Web.Proxy.StreamExtended.Network;

/// <summary>
/// The IHttpStreamWriter interface defines the contract for a writer that can write HTTP data to a stream.
/// It provides methods for writing bytes, writing a line, and writing a line with a specified value, either synchronously or asynchronously.
/// This interface is designed to be used in the context of network streams, where data is often written in a line-by-line manner.
/// </summary>
public interface IHttpStreamWriter
{
    /// <summary>
    /// Gets a value indicating whether the underlying stream is a network stream.
    /// </summary>
    bool IsNetworkStream { get; }

    /// <summary>
    /// Writes a sequence of bytes to the stream.
    /// </summary>
    /// <param name="buffer">The buffer to write data from.</param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the stream.</param>
    /// <param name="count">The number of bytes to be written to the stream.</param>
    void Write(byte[] buffer, int offset, int count);

    /// <summary>
    /// Asynchronously writes a sequence of bytes to the stream.
    /// </summary>
    /// <param name="buffer">The buffer to write data from.</param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the stream.</param>
    /// <param name="count">The number of bytes to be written to the stream.</param>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A Task that represents the asynchronous write operation.</returns>
    Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously writes a newline character to the stream.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A ValueTask that represents the asynchronous write operation.</returns>
    ValueTask WriteLineAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously writes a string followed by a newline character to the stream.
    /// </summary>
    /// <param name="value">The string to write to the stream.</param>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A ValueTask that represents the asynchronous write operation.</returns>
    ValueTask WriteLineAsync(string value, CancellationToken cancellationToken = default);
}