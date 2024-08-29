using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy;

/// <summary>
/// Default values for the proxy server
/// </summary>
public static class ProxyServerDefaults
{
    /// <summary>
    /// Default authentication realm
    /// </summary>
    public const string AuthenticationRealm = "UnobtaniumProxy";

    /// <summary>
    /// Default root certificate issuer name
    /// </summary>
    public const string RootCertificateIssuerName = "Unobtanium";

    /// <summary>
    /// Default root certificate name
    /// </summary>
    public const string RootCertificateName = "Unobtanium Root Certificate";

    /// <summary>
    /// Default supported SSL protocols
    /// </summary>
#pragma warning disable 618 // SslProtocols.Ssl3 is obsolete
#pragma warning disable SYSLIB0039 // Tls and Tls11 are obsolete
    public static readonly SslProtocols SslProtocols =
                SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;

#pragma warning restore SYSLIB0039 // Tls and Tls11 are obsolete
#pragma warning restore 618
}
