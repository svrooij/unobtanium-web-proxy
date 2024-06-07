namespace Titanium.Web.Proxy.Exceptions;

/// <summary>
/// If the request is send to the server, you can no longer access the request body.
/// </summary>
public class BodyLockedException : ProxyException
{
    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="message"></param>
    internal BodyLockedException(string message = "You cannot get the request body after request is made to server.") : base(message)
    {
    }
}