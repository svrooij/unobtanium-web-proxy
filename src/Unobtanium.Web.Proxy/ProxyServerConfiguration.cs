using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Models;

namespace Unobtanium.Web.Proxy;

/// <summary>
///    Configuration options for the proxy server.
/// </summary>
public class ProxyServerConfiguration
{

    /// <summary>
    ///     Realm used during Basic Authentication.
    /// </summary>
    public string AuthenticationRealm { get; set; } = ProxyServerDefaults.AuthenticationRealm;

    /// <summary>
    ///    Path to the folder where the root certificate and server certificates are stored.
    /// </summary>
    public string? CertificateCacheFolder { get; set; }

    /// <summary>
    /// Set this if you want to auto trust the root certificate
    /// </summary>
    public ProxyCertificateTrustMode CertificateTrustMode { get; set; }

    /// <summary>
    ///     Should we check for certificate revocation during SSL authentication to servers
    ///     Note: If enabled can reduce performance. Defaults to false.
    /// </summary>
    public X509RevocationMode CheckCertificateRevocation { get; set; }

    /// <summary>
    ///     Seconds client/server connection are to be kept alive when waiting for read/write to complete.
    ///     This will also determine the pool eviction time when connection pool is enabled.
    ///     Default value is 60 seconds.
    /// </summary>
    public int ConnectionTimeOutSeconds { get; set; } = 60;

    /// <summary>
    ///     Seconds server connection are to wait for connection to be established.
    ///     Default value is 20 seconds.
    /// </summary>
    public int ConnectTimeOutSeconds { get; set; } = 20;

    /// <summary>
    ///     Does this proxy uses the HTTP protocol 100 continue behaviour strictly?
    ///     Broken 100 continue implementations on server/client may cause problems if enabled.
    ///     Defaults to false.
    /// </summary>
    public bool Enable100ContinueBehaviour { get; set; }

    /// <summary>
    ///     Should we enable experimental server connection pool. Defaults to false.
    ///     When you enable connection pooling, instead of creating a new TCP connection to server for each client TCP
    ///     connection,
    ///     we check if a server connection is available in our cached pool. If it is available in our pool,
    ///     created from earlier requests to the same server, we will reuse those idle connections.
    ///     There is also a ConnectionTimeOutSeconds parameter, which determine the eviction time for inactive server
    ///     connections.
    ///     This will help to reduce TCP connection establishment cost, both the wall clock time and CPU cycles.
    /// </summary>
    public bool EnableConnectionPool { get; set; } = false;

    /// <summary>
    ///     Enable disable HTTP/2 support.
    ///     Warning: HTTP/2 support is very limited
    ///     - only enabled when both client and server supports it (no protocol changing in proxy)
    ///     - cannot modify the request/response (e.g header modifications in BeforeRequest/Response events are ignored)
    /// </summary>
    public bool EnableHttp2 { get; set; }

    /// <summary>
    ///     Should we enable tcp server connection prefetching?
    ///     When enabled, as soon as we receive a client connection we concurrently initiate
    ///     corresponding server connection process using CONNECT hostname or SNI hostname on a separate task so that after
    ///     parsing client request
    ///     we will have the server connection immediately ready or in the process of getting ready.
    ///     If a server connection is available in cache then this prefetch task will immediately return with the available
    ///     connection from cache.
    ///     Defaults to true.
    /// </summary>
    public bool EnableTcpServerConnectionPrefetch { get; set; } = true;

    /// <summary>
    ///     Enable disable Windows Authentication (NTLM/Kerberos).
    ///     Note: NTLM/Kerberos will always send local credentials of current user
    ///     running the proxy process. This is because a man
    ///     in middle attack with Windows domain authentication is not currently supported.
    ///     Defaults to false.
    /// </summary>
    public bool EnableWinAuth { get; set; }

    /// <summary>
    /// All configureable events on the proxy server.
    /// </summary>
    public Events.ProxyServerEvents Events { get; init; } = new Events.ProxyServerEvents();

    /// <summary>
    /// Configure the endpoints the proxy should listen to.
    /// </summary>
    public List<ProxyEndPoint>? EndPoints { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether requests will be chained to upstream gateway.
    ///     Defaults to false.
    /// </summary>
    public bool ForwardToUpstreamGateway { get; set; }

    /// <summary>
    ///     Maximum number of concurrent connections per remote host in cache.
    ///     Only valid when connection pooling is enabled.
    ///     Default value is 4.
    /// </summary>
    public int MaxCachedConnections { get; set; } = 4;

    /// <summary>
    ///     Number of times to retry upon network failures when connection pool is enabled.
    /// </summary>
    public int NetworkFailureRetryAttempts { get; set; } = 1;

    /// <summary>
    ///     Gets or sets a Boolean value that specifies whether server and client stream Sockets are using the Nagle algorithm.
    ///     Defaults to true, no nagle algorithm is used.
    /// </summary>
    public bool NoDelay { get; set; } = true;
    /// <summary>
    ///     Should we reuse client/server tcp sockets.
    ///     Default is true (disabled for linux/macOS due to bug in .Net core).
    /// </summary>
    public bool ReuseSocket { get; set; } = true;
    /// <summary>
    /// Name of the root certificate issuer
    /// </summary>
    public string RootCertificateIssuerName { get; set; } = ProxyServerDefaults.RootCertificateIssuerName;

    /// <summary>
    /// Name of the root certificate
    /// </summary>
    public string RootCertificateName { get; set; } = ProxyServerDefaults.RootCertificateName;

    /// <summary>
    /// You can set this funtion if you want to decide whether to proxy a request or not based on the request Uri.
    /// If you return false, the request will not be decrypted and will be sent as is to the server.
    /// </summary>
    /// <remarks>Usefull if you're having issues with certificate pinning or unsupported http versions.</remarks>
    public Func<Uri,CancellationToken,Task<bool>>? ShouldProxyRequest { get; set; }

    /// <summary>
    ///     List of supported Server Ssl versions.
    ///     Using SslProtocol.None means to require the same SSL protocol as the proxy client.
    /// </summary>
    public SslProtocols SupportedServerSslProtocols { get; set; } = SslProtocols.None;

    /// <summary>
    ///     List of supported Ssl versions.
    /// </summary>
    public SslProtocols SupportedSslProtocols { get; set; } = ProxyServerDefaults.SslProtocols;

    /// <summary>
    ///     Number of seconds to linger when Tcp connection is in TIME_WAIT state.
    ///     Default value is 30.
    /// </summary>
    public int TcpTimeWaitSeconds { get; set; } = 30;

    /// <summary>
    ///     If set, the upstream proxy will be detected by a script that will be loaded from the provided Uri
    /// </summary>
    public Uri? UpstreamProxyConfigurationScript { get; set; }
}

/// <summary>
/// Certificate trust mode
/// </summary>
[Flags]
public enum ProxyCertificateTrustMode
{
    /// <summary>
    /// Do not auto trust any certificate
    /// </summary>
    None = 0,
    /// <summary>
    /// Add the root certificate to the user trust store
    /// </summary>
    UserTrust = 1,
    /// <summary>
    /// Add the root certificate to the machine trust store
    /// </summary>
    MachineTrust = 2,
    /// <summary>
    /// Try to add the root certificate to the machine trust store using UAC
    /// </summary>
    TryWithUac = 4
}
