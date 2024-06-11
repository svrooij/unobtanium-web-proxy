using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.EventArguments;

namespace Titanium.Web.Proxy.StreamExtended.Network;

/// <summary>
/// The IHttpStreamReader interface defines the contract for a reader that can read HTTP data from a stream.
/// It provides methods for reading bytes and copying the body of an HTTP message, either synchronously or asynchronously.
/// This interface is designed to be used in the context of network streams, where data is often read in a line-by-line manner.
/// </summary>
public interface IHttpStreamReader : ILineStream
{
    ///// <summary>
    ///// Reads a sequence of bytes from the stream.
    ///// </summary>
    ///// <param name="buffer">The buffer to write data into.</param>
    ///// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the stream.</param>
    ///// <param name="count">The maximum number of bytes to be read from the stream.</param>
    ///// <returns>The total number of bytes read into the buffer.</returns>
    //int Read(byte[] buffer, int offset, int count);

    /// <summary>
    /// Asynchronously reads a sequence of bytes from the stream.
    /// </summary>
    /// <param name="buffer">The buffer to write data into.</param>
    /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the stream.</param>
    /// <param name="count">The maximum number of bytes to be read from the stream.</param>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A Task that represents the asynchronous read operation. The value of the TResult parameter contains the total number of bytes read into the buffer.</returns>
    Task<int> ReadAsync ( byte[] buffer, int offset, int count, CancellationToken cancellationToken );

    /// <summary>
    /// Asynchronously copies the body of an HTTP message to a writer.
    /// </summary>
    /// <param name="writer">The IHttpStreamWriter to which the body should be copied.</param>
    /// <param name="isChunked">A boolean value indicating whether the body is chunked.</param>
    /// <param name="contentLength">The length of the content to be copied.</param>
    /// <param name="isRequest">A boolean value indicating whether the body is a request.</param>
    /// <param name="args">The SessionEventArgs associated with the HTTP session.</param>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A Task that represents the asynchronous copy operation.</returns>
    Task CopyBodyAsync ( IHttpStreamWriter writer, bool isChunked, long contentLength,
        bool isRequest, SessionEventArgs args, CancellationToken cancellationToken );
}
