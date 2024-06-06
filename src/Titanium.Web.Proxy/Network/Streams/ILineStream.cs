using System.Threading;
using System.Threading.Tasks;

namespace Titanium.Web.Proxy.StreamExtended.Network;
/// <summary>
/// The ILineStream interface defines the contract for a stream that supports line-by-line reading of data.
/// It provides methods for filling a buffer asynchronously, reading a byte from the buffer, and reading a line asynchronously.
/// This interface is designed to be used in the context of network streams, where data is often read in a line-by-line manner.
/// </summary>
public interface ILineStream
{
    /// <summary>
    /// Gets a value indicating whether data is available in the internal buffer.
    /// </summary>
    bool DataAvailable { get; }

    /// <summary>
    /// Asynchronously fills the internal buffer with data from the stream.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A ValueTask that completes when the buffer has been filled, yielding a boolean indicating whether any data was read.</returns>
    ValueTask<bool> FillBufferAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a single byte from the internal buffer.
    /// </summary>
    /// <returns>The byte read from the buffer.</returns>
    byte ReadByteFromBuffer();

    /// <summary>
    /// Asynchronously reads a line of data from the stream.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A ValueTask that completes when the line has been read, yielding the line as a string, or null if the end of the stream has been reached.</returns>
    ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken = default);
}