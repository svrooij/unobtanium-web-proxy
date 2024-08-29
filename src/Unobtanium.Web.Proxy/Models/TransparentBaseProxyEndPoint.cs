using System.Net;
using System.Threading.Tasks;
using Titanium.Web.Proxy.EventArguments;

namespace Titanium.Web.Proxy.Models;

/// <summary>
/// The TransparentBaseProxyEndPoint class is an abstract base class for endpoints that support transparent proxying.
/// It inherits from the ProxyEndPoint class and provides additional functionality specific to transparent proxying,
/// such as SSL decryption and handling of SSL authentication.
/// </summary>
public abstract class TransparentBaseProxyEndPoint : ProxyEndPoint
{
    /// <summary>
    /// Initializes a new instance of the TransparentBaseProxyEndPoint class with the specified IP address, port, and SSL decryption setting.
    /// </summary>
    /// <param name="ipAddress">The IP address of the endpoint.</param>
    /// <param name="port">The port of the endpoint.</param>
    /// <param name="decryptSsl">A boolean value that indicates whether SSL decryption is enabled for this endpoint.</param>
    protected TransparentBaseProxyEndPoint ( IPAddress ipAddress, int port, bool decryptSsl ) : base(ipAddress, port,
        decryptSsl)
    {
    }

    /// <summary>
    ///     The hostname of the generic certificate to negotiate SSL.
    ///     This will be only used when Sever Name Indication (SNI) is not supported by client,
    ///     or when it does not indicate any host name.
    /// </summary>
    public abstract string GenericCertificateName { get; set; }

    internal abstract Task InvokeBeforeSslAuthenticate ( ProxyServer proxyServer,
        BeforeSslAuthenticateEventArgs connectArgs, ExceptionHandler? exceptionFunc );
}
