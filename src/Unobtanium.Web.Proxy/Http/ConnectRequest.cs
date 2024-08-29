using Unobtanium.Web.Proxy.Models;
using Unobtanium.Web.Proxy.StreamExtended;

namespace Unobtanium.Web.Proxy.Http;

/// <summary>
///     The tcp tunnel Connect request.
/// </summary>
public class ConnectRequest : Request
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectRequest"/> class.
    /// </summary>
    /// <param name="authority">The authority.</param>
    internal ConnectRequest ( ByteString authority )
    {
        Method = "CONNECT";
        Authority = authority;
    }

    /// <summary>
    /// Gets or sets the type of the tunnel.
    /// </summary>
    public TunnelType TunnelType { get; internal set; }

    /// <summary>
    /// Gets or sets the client hello information.
    /// </summary>
    public ClientHelloInfo? ClientHelloInfo { get; set; }
}
