namespace Titanium.Web.Proxy.Exceptions;

/// <summary>
/// Body not found exception, is thrown in case the body is not found
/// or it's not a POST/PUT/PATCH request
/// or the content-length is zero.
/// </summary>
public class BodyNotFoundException : ProxyException
{
    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="message"></param>
    internal BodyNotFoundException ( string message = "Request does not have a body" ) : base(message)
    {
    }
}