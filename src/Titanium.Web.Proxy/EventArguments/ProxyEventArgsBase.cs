using System;
using Titanium.Web.Proxy.Network.Tcp;

namespace Titanium.Web.Proxy.EventArguments;

/// <summary>
///     The base event arguments.
/// </summary>
/// <seealso cref="System.EventArgs" />
public abstract class ProxyEventArgsBase : EventArgs
{
    private readonly TcpClientConnection clientConnection;
    internal readonly ProxyServer Server;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProxyEventArgsBase"/> class.
    /// </summary>
    /// <param name="server">The proxy server instance.</param>
    /// <param name="clientConnection">The client connection.</param>
    internal ProxyEventArgsBase ( ProxyServer server, TcpClientConnection clientConnection )
    {
        this.clientConnection = clientConnection;
        Server = server;
    }

    /// <summary>
    /// Gets or sets the user data associated with the client.
    /// </summary>
    public object? ClientUserData
    {
        get => clientConnection.ClientUserData;
        set => clientConnection.ClientUserData = value;
    }
}