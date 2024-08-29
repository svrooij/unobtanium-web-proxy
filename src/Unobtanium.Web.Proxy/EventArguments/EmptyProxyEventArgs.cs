using Unobtanium.Web.Proxy.Network.Tcp;

namespace Unobtanium.Web.Proxy.EventArguments;

/// <summary>
/// Represents the arguments for an empty proxy event.
/// </summary>
public class EmptyProxyEventArgs : ProxyEventArgsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyProxyEventArgs"/> class.
    /// </summary>
    /// <param name="server">The proxy server instance.</param>
    /// <param name="clientConnection">The client connection.</param>
    internal EmptyProxyEventArgs ( ProxyServer server, TcpClientConnection clientConnection ) : base(server,
        clientConnection)
    {
    }
}
