using Titanium.Web.Proxy.Http;

namespace Titanium.Web.Proxy.EventArguments;

/// <summary>
/// Class that wraps the multipart sent request arguments.
/// </summary>
public class MultipartRequestPartSentEventArgs : ProxyEventArgsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipartRequestPartSentEventArgs"/> class.
    /// </summary>
    /// <param name="session">The session event arguments.</param>
    /// <param name="boundary">The multipart boundary.</param>
    /// <param name="headers">The multipart headers.</param>
    internal MultipartRequestPartSentEventArgs ( SessionEventArgs session, string boundary, HeaderCollection headers ) :
        base(session.Server, session.ClientConnection)
    {
        Session = session;
        Boundary = boundary;
        Headers = headers;
    }

    /// <summary>
    /// Gets the session arguments.
    /// </summary>
    public SessionEventArgs Session { get; }

    /// <summary>
    /// Gets the multipart boundary.
    /// </summary>
    public string Boundary { get; }

    /// <summary>
    /// Gets the multipart headers.
    /// </summary>
    public HeaderCollection Headers { get; }
}