namespace Titanium.Web.Proxy.Exceptions;

/// <summary>
/// Body not loaded is thrown when you did not load it correctly.
/// </summary>
/// <remarks>This issue is thrown if you did not call any of these methods:
/// - <see cref="Titanium.Web.Proxy.EventArguments.SessionEventArgs.GetRequestBody(System.Threading.CancellationToken)"/>
/// - <see cref="Titanium.Web.Proxy.EventArguments.SessionEventArgs.GetRequestBodyAsString(System.Threading.CancellationToken)"/>
/// - <see cref="Titanium.Web.Proxy.EventArguments.SessionEventArgs.GetResponseBody(System.Threading.CancellationToken)"/>
/// - <see cref="Titanium.Web.Proxy.EventArguments.SessionEventArgs.GetResponseBodyAsString(System.Threading.CancellationToken)"/>
/// </remarks>
public class BodyNotLoadedException : ProxyException
{
    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="message"></param>
    internal BodyNotLoadedException ( string message = "Body is not loaded yet. Make sure you'll call the appropriate method" ) : base(message)
    {
    }
}