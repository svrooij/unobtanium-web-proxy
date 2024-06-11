﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Extensions;
using Titanium.Web.Proxy.Helpers;
using Titanium.Web.Proxy.Helpers.WinHttp;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Network;
using Titanium.Web.Proxy.Network.Tcp;
using Titanium.Web.Proxy.StreamExtended.BufferPool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace Titanium.Web.Proxy;

/// <summary>
///     This class is the backbone of proxy. One can create as many instances as needed.
///     However care should be taken to avoid using the same listening ports across multiple instances.
/// </summary>
public partial class ProxyServer : IDisposable
{
    /// <summary>
    ///     HTTP &amp; HTTPS scheme shorthands.
    /// </summary>
    internal static readonly string UriSchemeHttp = Uri.UriSchemeHttp;

    internal static readonly string UriSchemeHttps = Uri.UriSchemeHttps;

    internal static ByteString UriSchemeHttp8 = (ByteString)UriSchemeHttp;
    internal static ByteString UriSchemeHttps8 = (ByteString)UriSchemeHttps;

    /// <summary>
    ///     Backing field for exposed public property.
    /// </summary>
    private int clientConnectionCount;

    /// <summary>
    ///     Backing field for exposed public property.
    /// </summary>
    private ExceptionHandler? exceptionFunc;

    /// <summary>
    ///     Backing field for exposed public property.
    /// </summary>
    private int serverConnectionCount;

    /// <summary>
    ///     Upstream proxy manager.
    /// </summary>
    private WinHttpWebProxyFinder? systemProxyResolver;

    /// <summary>
    /// <see cref="ActivitySource"/> that can be supplied to support distributed tracing
    /// </summary>
    internal readonly ActivitySource? activitySource;

    /// <summary>
    /// Logger factory to create loggers for various types
    /// </summary>
    internal readonly ILoggerFactory loggerFactory;
    
    private readonly ILogger<ProxyServer> logger;

    /// <inheritdoc />
    /// <summary>
    ///     Initializes a new instance of ProxyServer class with provided parameters.
    /// </summary>
    /// <param name="userTrustRootCertificate">
    ///     Should fake HTTPS certificate be trusted by this machine's user certificate
    ///     store?
    /// </param>
    /// <param name="machineTrustRootCertificate">Should fake HTTPS certificate be trusted by this machine's certificate store?</param>
    /// <param name="trustRootCertificateAsAdmin">
    ///     Should we attempt to trust certificates with elevated permissions by
    ///     prompting for UAC if required?
    /// </param>
    /// <param name="activitySource"><see cref="ActivitySource"/> to be used for distributed tracing</param>
    /// <param name="loggerFactory"><see cref="ILoggerFactory"/> to be used for all logging, will use <see cref="NullLoggerFactory"/> is not specified</param>
    public ProxyServer(bool userTrustRootCertificate = true, bool machineTrustRootCertificate = false,
        bool trustRootCertificateAsAdmin = false, ActivitySource? activitySource = null, ILoggerFactory? loggerFactory = null) : this(null, null, userTrustRootCertificate,
        machineTrustRootCertificate, trustRootCertificateAsAdmin, activitySource, loggerFactory)
    {
    }

    /// <summary>
    ///     Initializes a new instance of ProxyServer class with provided parameters.
    /// </summary>
    /// <param name="rootCertificateName">Name of the root certificate.</param>
    /// <param name="rootCertificateIssuerName">Name of the root certificate issuer.</param>
    /// <param name="userTrustRootCertificate">
    ///     Should fake HTTPS certificate be trusted by this machine's user certificate
    ///     store?
    /// </param>
    /// <param name="machineTrustRootCertificate">Should fake HTTPS certificate be trusted by this machine's certificate store?</param>
    /// <param name="trustRootCertificateAsAdmin">
    ///     Should we attempt to trust certificates with elevated permissions by
    ///     prompting for UAC if required?
    /// </param>
    /// <param name="activitySource"><see cref="ActivitySource"/> to be used for distributed tracing</param>
    /// <param name="loggerFactory"><see cref="ILoggerFactory"/> to be used for all logging, will use <see cref="NullLoggerFactory"/> is not specified</param>
    public ProxyServer(string? rootCertificateName, string? rootCertificateIssuerName,
        bool userTrustRootCertificate = true, bool machineTrustRootCertificate = false,
        bool trustRootCertificateAsAdmin = false, ActivitySource? activitySource = null, ILoggerFactory? loggerFactory = null)
    {
        this.activitySource = activitySource;
        this.loggerFactory = loggerFactory ?? new NullLoggerFactory();
        logger = this.loggerFactory.CreateLogger<ProxyServer>();

        BufferPool = new DefaultBufferPool();
        ProxyEndPoints = [];
        TcpConnectionFactory = new TcpConnectionFactory(this);
        if (RunTime.IsWindows && !RunTime.IsUwpOnWindows) SystemProxySettingsManager = new SystemProxyManager();

        CertificateManager = new CertificateManager(rootCertificateName, rootCertificateIssuerName,
            userTrustRootCertificate, machineTrustRootCertificate, trustRootCertificateAsAdmin, ExceptionFunc);
    }

    /// <summary>
    ///     An factory that creates tcp connection to server.
    /// </summary>
    private TcpConnectionFactory TcpConnectionFactory { get; }

    /// <summary>
    ///     Manage system proxy settings.
    /// </summary>
    private SystemProxyManager? SystemProxySettingsManager { get; }

    /// <summary>
    ///     Number of times to retry upon network failures when connection pool is enabled.
    /// </summary>
    public int NetworkFailureRetryAttempts { get; set; } = 1;

    /// <summary>
    ///     Is the proxy currently running?
    /// </summary>
    public bool ProxyRunning { get; private set; }

    /// <summary>
    ///     Gets or sets a value indicating whether requests will be chained to upstream gateway.
    ///     Defaults to false.
    /// </summary>
    public bool ForwardToUpstreamGateway { get; set; }

    /// <summary>
    ///     If set, the upstream proxy will be detected by a script that will be loaded from the provided Uri
    /// </summary>
    public Uri? UpstreamProxyConfigurationScript { get; set; }

    /// <summary>
    ///     Enable disable Windows Authentication (NTLM/Kerberos).
    ///     Note: NTLM/Kerberos will always send local credentials of current user
    ///     running the proxy process. This is because a man
    ///     in middle attack with Windows domain authentication is not currently supported.
    ///     Defaults to false.
    /// </summary>
    public bool EnableWinAuth { get; set; }

    /// <summary>
    ///     Enable disable HTTP/2 support.
    ///     Warning: HTTP/2 support is very limited
    ///     - only enabled when both client and server supports it (no protocol changing in proxy)
    ///     - cannot modify the request/response (e.g header modifications in BeforeRequest/Response events are ignored)
    /// </summary>
    public bool EnableHttp2 { get; set; } = false;

    /// <summary>
    ///     Should we check for certificate revocation during SSL authentication to servers
    ///     Note: If enabled can reduce performance. Defaults to false.
    /// </summary>
    public X509RevocationMode CheckCertificateRevocation { get; set; }

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
    ///     Gets or sets a Boolean value that specifies whether server and client stream Sockets are using the Nagle algorithm.
    ///     Defaults to true, no nagle algorithm is used.
    /// </summary>
    public bool NoDelay { get; set; } = true;

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
    ///     Maximum number of concurrent connections per remote host in cache.
    ///     Only valid when connection pooling is enabled.
    ///     Default value is 4.
    /// </summary>
    public int MaxCachedConnections { get; set; } = 4;

    /// <summary>
    ///     Number of seconds to linger when Tcp connection is in TIME_WAIT state.
    ///     Default value is 30.
    /// </summary>
    public int TcpTimeWaitSeconds { get; set; } = 30;

    /// <summary>
    ///     Should we reuse client/server tcp sockets.
    ///     Default is true (disabled for linux/macOS due to bug in .Net core).
    /// </summary>
    public bool ReuseSocket { get; set; } = true;

    /// <summary>
    ///     Total number of active client connections.
    /// </summary>
    public int ClientConnectionCount => clientConnectionCount;

    /// <summary>
    ///     Total number of active server connections.
    /// </summary>
    public int ServerConnectionCount => serverConnectionCount;

    /// <summary>
    ///     Realm used during Proxy Basic Authentication.
    /// </summary>
    public string ProxyAuthenticationRealm { get; set; } = "TitaniumProxy";

    /// <summary>
    ///     List of supported Ssl versions.
    /// </summary>
#pragma warning disable 618
    public SslProtocols SupportedSslProtocols { get; set; } =
        SslProtocols.Ssl3 | SslProtocols.Tls12 | SslProtocols.Tls13 ;
#pragma warning restore 618

    /// <summary>
    ///     List of supported Server Ssl versions.
    ///     Using SslProtocol.None means to require the same SSL protocol as the proxy client.
    /// </summary>
#pragma warning disable 618
    public SslProtocols SupportedServerSslProtocols { get; set; } = SslProtocols.None;
#pragma warning restore 618

    /// <summary>
    ///     The buffer pool used throughout this proxy instance.
    ///     Set custom implementations by implementing this interface.
    ///     By default this uses DefaultBufferPool implementation available in StreamExtended library package.
    ///     Buffer size should be at least 10 bytes.
    /// </summary>
    public IBufferPool BufferPool { get; set; }

    /// <summary>
    ///     Manages certificates used by this proxy.
    /// </summary>
    public CertificateManager CertificateManager { get; }

    /// <summary>
    ///     External proxy used for Http requests.
    /// </summary>
    public IExternalProxy? UpStreamHttpProxy { get; set; }

    /// <summary>
    ///     External proxy used for Https requests.
    /// </summary>
    public IExternalProxy? UpStreamHttpsProxy { get; set; }

    /// <summary>
    ///     Local adapter/NIC endpoint where proxy makes request via.
    ///     Defaults via any IP addresses of this machine.
    /// </summary>
    public IPEndPoint? UpStreamEndPoint { get; set; }

    /// <summary>
    ///     A list of IpAddress and port this proxy is listening to.
    /// </summary>
    public List<ProxyEndPoint> ProxyEndPoints { get; set; }

    /// <summary>
    ///     A callback to provide authentication credentials for up stream proxy this proxy is using for HTTP(S) requests.
    ///     User should return the ExternalProxy object with valid credentials.
    /// </summary>
    public Func<SessionEventArgsBase, Task<IExternalProxy?>>? GetCustomUpStreamProxyFunc { get; set; }

    /// <summary>
    ///     A callback to provide a chance for an upstream proxy failure to be handled by a new upstream proxy.
    ///     User should return the ExternalProxy object with valid credentials or null.
    /// </summary>
    public Func<SessionEventArgsBase, Task<IExternalProxy?>>? CustomUpStreamProxyFailureFunc { get; set; }

    /// <summary>
    ///     Callback for error events in this proxy instance.
    /// </summary>
    public ExceptionHandler? ExceptionFunc
    {
        get => exceptionFunc;
        set
        {
            exceptionFunc = value;
            CertificateManager.ExceptionFunc = value;
        }
    }

    /// <summary>
    ///     A callback to authenticate proxy clients via basic authentication.
    ///     Parameters are username and password as provided by client.
    ///     Should return true for successful authentication.
    /// </summary>
    public Func<SessionEventArgsBase?, string, string, Task<bool>>? ProxyBasicAuthenticateFunc { get; set; }

    /// <summary>
    ///     A pluggable callback to authenticate clients by scheme instead of requiring basic authentication through
    ///     ProxyBasicAuthenticateFunc.
    ///     Parameters are current working session, schemeType, and token as provided by a calling client.
    ///     Should return success for successful authentication, continuation if the package requests, or failure.
    /// </summary>
    public Func<SessionEventArgsBase?, string, string, Task<ProxyAuthenticationContext>>? ProxySchemeAuthenticateFunc
    {
        get;
        set;
    }

    /// <summary>
    ///     A collection of scheme types, e.g. basic, NTLM, Kerberos, Negotiate, to return if scheme authentication is
    ///     required.
    ///     Works in relation with ProxySchemeAuthenticateFunc.
    /// </summary>
    public IEnumerable<string> ProxyAuthenticationSchemes { get; set; } = [];

    /// <summary>
    ///     Event occurs when client connection count changed.
    /// </summary>
    public event EventHandler? ClientConnectionCountChanged;

    /// <summary>
    ///     Event occurs when server connection count changed.
    /// </summary>
    public event EventHandler? ServerConnectionCountChanged;

    /// <summary>
    ///     Event to override the default verification logic of remote SSL certificate received during authentication.
    /// </summary>
    public event AsyncEventHandler<CertificateValidationEventArgs>? ServerCertificateValidationCallback;

    /// <summary>
    ///     Event to override client certificate selection during mutual SSL authentication.
    /// </summary>
    public event AsyncEventHandler<CertificateSelectionEventArgs>? ClientCertificateSelectionCallback;

    /// <summary>
    ///     Intercept request event to server.
    /// </summary>
    public event AsyncEventHandler<SessionEventArgs>? BeforeRequest;

#if DEBUG
        /// <summary>
        ///     Intercept request body send event to server. 
        /// </summary>
        public event AsyncEventHandler<BeforeBodyWriteEventArgs>? OnRequestBodyWrite;
#endif
    /// <summary>
    ///     Intercept response event from server.
    /// </summary>
    public event AsyncEventHandler<SessionEventArgs>? BeforeResponse;

#if DEBUG
        /// <summary>
        ///     Intercept request body send event to client. 
        /// </summary>
        public event AsyncEventHandler<BeforeBodyWriteEventArgs>? OnResponseBodyWrite;
#endif
    /// <summary>
    ///     Intercept after response event from server.
    /// </summary>
    public event AsyncEventHandler<SessionEventArgs>? AfterResponse;

    /// <summary>
    ///     Customize TcpClient used for client connection upon create.
    /// </summary>
    public event AsyncEventHandler<Socket>? OnClientConnectionCreate;

    /// <summary>
    ///     Customize TcpClient used for server connection upon create.
    /// </summary>
    public event AsyncEventHandler<Socket>? OnServerConnectionCreate;

    /// <summary>
    ///     Intercept connect request sent to upstream proxy.
    /// </summary>
    public event AsyncEventHandler<ConnectRequest>? BeforeUpStreamConnectRequest;

    /// <summary>
    ///     Customize the minimum ThreadPool size (increase it on a server)
    /// </summary>
    public int ThreadPoolWorkerThread { get; set; } = Environment.ProcessorCount;

    /// <summary>
    ///     Add a proxy end point.
    /// </summary>
    /// <param name="endPoint">The proxy endpoint.</param>
    public void AddEndPoint(ProxyEndPoint endPoint)
    {
        if (ProxyEndPoints.Any(x =>
                x.IpAddress.Equals(endPoint.IpAddress) && endPoint.Port != 0 && x.Port == endPoint.Port))
        {
            var ex =  new Exception("Cannot add another endpoint to same port & ip address");
            logger.LogWarning(ex, "Cannot add another endpoint to same port & ip address");
            throw ex;
        }
            
        logger.LogInformation("Adding endpoint at Ip {IpAddress} and port: {Port}", endPoint.IpAddress, endPoint.Port);
        ProxyEndPoints.Add(endPoint);

        if (ProxyRunning) Listen(endPoint);
    }

    /// <summary>
    ///     Remove a proxy end point.
    ///     Will throw error if the end point doesn't exist.
    /// </summary>
    /// <param name="endPoint">The existing endpoint to remove.</param>
    public void RemoveEndPoint(ProxyEndPoint endPoint)
    {
        if (ProxyEndPoints.Contains(endPoint) == false)
        {
            var ex = new Exception("Cannot remove endPoints not added to proxy");

            logger.LogWarning(ex, "Cannot remove endPoints not added to proxy");
            throw ex;
        }
            
        logger.LogInformation("Removing endpoint at Ip {IpAddress} and port: {Port}", endPoint.IpAddress, endPoint.Port);
        ProxyEndPoints.Remove(endPoint);

        if (ProxyRunning) QuitListen(endPoint);
    }

    /// <summary>
    ///     Set the given explicit end point as the default proxy server for current machine.
    /// </summary>
    /// <param name="endPoint">The explicit endpoint.</param>
    public void SetAsSystemHttpProxy(ExplicitProxyEndPoint endPoint)
    {
        SetAsSystemProxy(endPoint, ProxyProtocolType.Http);
    }

    /// <summary>
    ///     Set the given explicit end point as the default proxy server for current machine.
    /// </summary>
    /// <param name="endPoint">The explicit endpoint.</param>
    public void SetAsSystemHttpsProxy(ExplicitProxyEndPoint endPoint)
    {
        SetAsSystemProxy(endPoint, ProxyProtocolType.Https);
    }

    /// <summary>
    ///     Set the given explicit end point as the default proxy server for current machine.
    /// </summary>
    /// <param name="endPoint">The explicit endpoint.</param>
    /// <param name="protocolType">The proxy protocol type.</param>
    public void SetAsSystemProxy(ExplicitProxyEndPoint endPoint, ProxyProtocolType protocolType)
    {
        if (SystemProxySettingsManager == null)
        {
            ThrowNotSupportedException();
            return;
        }


        ValidateEndPointAsSystemProxy(endPoint);

        var isHttp = (protocolType & ProxyProtocolType.Http) > 0;
        var isHttps = (protocolType & ProxyProtocolType.Https) > 0;

        if (isHttps)
        {
            CertificateManager.EnsureRootCertificate();

            // If certificate was trusted by the machine
            if (!CertificateManager.CertValidated)
            {
                protocolType &= ~ProxyProtocolType.Https;
                isHttps = false;
            }
        }

        // clear any settings previously added
        if (isHttp) ProxyEndPoints.OfType<ExplicitProxyEndPoint>().ToList().ForEach(x => x.IsSystemHttpProxy = false);

        if (isHttps) ProxyEndPoints.OfType<ExplicitProxyEndPoint>().ToList().ForEach(x => x.IsSystemHttpsProxy = false);

        SystemProxySettingsManager.SetProxy(
            Equals(endPoint.IpAddress, IPAddress.Any) |
            Equals(endPoint.IpAddress, IPAddress.Loopback)
                ? "localhost"
                : endPoint.IpAddress.ToString(),
            endPoint.Port,
            protocolType);

        if (isHttp) endPoint.IsSystemHttpProxy = true;

        if (isHttps) endPoint.IsSystemHttpsProxy = true;

        string? proxyType = null;
        switch (protocolType)
        {
            case ProxyProtocolType.Http:
                proxyType = "HTTP";
                break;
            case ProxyProtocolType.Https:
                proxyType = "HTTPS";
                break;
            case ProxyProtocolType.AllHttp:
                proxyType = "HTTP and HTTPS";
                break;
        }

        if (protocolType != ProxyProtocolType.None)
        {
            logger.LogInformation("Set endpoint at Ip {ip} and port: {port} as System {proxyType} Proxy", endPoint.IpAddress,
                endPoint.Port, proxyType);
        }
            
    }

    /// <summary>
    ///     Clear HTTP proxy settings of current machine.
    /// </summary>
    public void DisableSystemHttpProxy()
    {
        DisableSystemProxy(ProxyProtocolType.Http);
    }

    /// <summary>
    ///     Clear HTTPS proxy settings of current machine.
    /// </summary>
    public void DisableSystemHttpsProxy()
    {
        DisableSystemProxy(ProxyProtocolType.Https);
    }

    /// <summary>
    ///     Restores the original proxy settings.
    /// </summary>
    public void RestoreOriginalProxySettings()
    {
        if (SystemProxySettingsManager == null)
        {
            ThrowNotSupportedException();
            return;
        }

        SystemProxySettingsManager.RestoreOriginalSettings();
    }

    /// <summary>
    ///     Clear the specified proxy setting for current machine.
    /// </summary>
    public void DisableSystemProxy(ProxyProtocolType protocolType)
    {
        if (SystemProxySettingsManager == null)
        {
            ThrowNotSupportedException();
            return;
        }

        SystemProxySettingsManager.RemoveProxy(protocolType);
    }

    /// <summary>
    ///     Clear all proxy settings for current machine.
    /// </summary>
    public void DisableAllSystemProxies()
    {
        if (SystemProxySettingsManager == null)
            throw new NotSupportedException(@"Setting system proxy settings are only supported in Windows.
                            Please manually confugure you operating system to use this proxy's port and address.");

        SystemProxySettingsManager.DisableAllProxy();
    }

    /// <summary>
    ///     Start this proxy server instance.
    /// </summary>
    /// <param name="changeSystemProxySettings">
    ///     Whether or not clear any system proxy settings which is pointing to our own endpoint (causing a cycle).
    ///     E.g due to ungracious proxy shutdown before.
    /// </param>
    public void Start(bool changeSystemProxySettings = true)
    {
        if (ProxyRunning) throw new Exception("Proxy is already running.");

        SetThreadPoolMinThread(ThreadPoolWorkerThread);

        if (ProxyEndPoints.OfType<ExplicitProxyEndPoint>().Any(x => x.GenericCertificate == null))
            CertificateManager.EnsureRootCertificate();

        if (changeSystemProxySettings && SystemProxySettingsManager != null && RunTime.IsWindows &&
            !RunTime.IsUwpOnWindows)
        {
            var proxyInfo = SystemProxySettingsManager.GetProxyInfoFromRegistry();
            if (proxyInfo?.Proxies != null)
            {
                var protocolToRemove = ProxyProtocolType.None;
                foreach (var proxy in proxyInfo.Proxies.Values)
                    if (NetworkHelper.IsLocalIpAddress(proxy.HostName)
                        && ProxyEndPoints.Any(x => x.Port == proxy.Port))
                        protocolToRemove |= proxy.ProtocolType;

                if (protocolToRemove != ProxyProtocolType.None)
                    SystemProxySettingsManager.RemoveProxy(protocolToRemove, false);
            }
        }

        if (ForwardToUpstreamGateway && GetCustomUpStreamProxyFunc == null && SystemProxySettingsManager != null)
        {
            systemProxyResolver = new WinHttpWebProxyFinder();
            if (UpstreamProxyConfigurationScript != null)
                //Use the provided proxy configuration script
                systemProxyResolver.UsePacFile(UpstreamProxyConfigurationScript);
            else
                // Use WinHttp to handle PAC/WAPD scripts.
                systemProxyResolver.LoadFromIe();

            GetCustomUpStreamProxyFunc = GetSystemUpStreamProxy;
        }

        ProxyRunning = true;

        CertificateManager.ClearIdleCertificates();

        foreach (var endPoint in ProxyEndPoints) Listen(endPoint);
    }

    /// <summary>
    ///     Stop this proxy server instance.
    /// </summary>
    public void Stop()
    {
        if (!ProxyRunning) throw new Exception("Proxy is not running.");

        if (SystemProxySettingsManager != null)
        {
            var setAsSystemProxy = ProxyEndPoints.OfType<ExplicitProxyEndPoint>()
                .Any(x => x.IsSystemHttpProxy || x.IsSystemHttpsProxy);

            if (setAsSystemProxy) SystemProxySettingsManager.RestoreOriginalSettings();
        }

        foreach (var endPoint in ProxyEndPoints) QuitListen(endPoint);

        ProxyEndPoints.Clear();

        CertificateManager?.StopClearIdleCertificates();
        TcpConnectionFactory.Dispose();

        ProxyRunning = false;
    }

    /// <summary>
    ///     Listen on given end point of local machine.
    /// </summary>
    /// <param name="endPoint">The end point to listen.</param>
    private void Listen(ProxyEndPoint endPoint)
    {
        endPoint.Listener = new TcpListener(endPoint.IpAddress, endPoint.Port);

        if (ReuseSocket && RunTime.IsSocketReuseAvailable())
            endPoint.Listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try
        {
            endPoint.Listener.Start();

            endPoint.Port = ((IPEndPoint)endPoint.Listener.LocalEndpoint).Port;

            // accept clients asynchronously
            endPoint.Listener.BeginAcceptSocket(OnAcceptConnection, endPoint);
        }
        catch (SocketException ex)
        {
            var pex = new Exception(
                $"Endpoint {endPoint} failed to start. Check inner exception and exception data for details.", ex);
            pex.Data.Add("ipAddress", endPoint.IpAddress);
            pex.Data.Add("port", endPoint.Port);
            throw pex;
        }
    }

    /// <summary>
    ///     Verify if its safe to set this end point as system proxy.
    /// </summary>
    /// <param name="endPoint">The end point to validate.</param>
    private void ValidateEndPointAsSystemProxy(ExplicitProxyEndPoint endPoint)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        if (!ProxyEndPoints.Contains(endPoint))
            throw new Exception("Cannot set endPoints not added to proxy as system proxy");

        if (!ProxyRunning) throw new Exception("Cannot set system proxy settings before proxy has been started.");
    }

    /// <summary>
    ///     Gets the system up stream proxy.
    /// </summary>
    /// <param name="sessionEventArgs">The session.</param>
    /// <returns>The external proxy as task result.</returns>
    private Task<IExternalProxy?> GetSystemUpStreamProxy(SessionEventArgsBase sessionEventArgs)
    {
        var proxy = systemProxyResolver!.GetProxy(sessionEventArgs.HttpClient.Request.RequestUri);
        return Task.FromResult(proxy);
    }

    /// <summary>
    ///     Act when a connection is received from client.
    /// </summary>
    private void OnAcceptConnection(IAsyncResult asyn)
    {
        var endPoint = asyn.AsyncState as ProxyEndPoint;

        Socket? tcpClient = null;

        try
        {
            // based on end point type call appropriate request handlers
            tcpClient = endPoint!.Listener!.EndAcceptSocket(asyn);
            tcpClient.NoDelay = NoDelay;
        }
        catch (ObjectDisposedException)
        {
            // The listener was Stop()'d, disposing the underlying socket and
            // triggering the completion of the callback. We're already exiting,
            // so just return.
            return;
        }
        catch
        {
            // Other errors are discarded to keep proxy running
        }

        if (tcpClient != null)
            Task.Run(async () => { await HandleClient(tcpClient, endPoint!); });

        try
        {
            // based on end point type call appropriate request handlers
            // Get the listener that handles the client request.
            endPoint!.Listener!.BeginAcceptSocket(OnAcceptConnection, endPoint);
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
        {
            // The listener was Stop()'d, disposing the underlying socket and
            // triggering the completion of the callback. We're already exiting,
            // so just return.
        }
    }


    /// <summary>
    ///     Change the ThreadPool.WorkerThread minThread
    /// </summary>
    /// <param name="workerThreads">minimum Threads allocated in the ThreadPool</param>
    private void SetThreadPoolMinThread(int workerThreads)
    {
        ThreadPool.GetMinThreads(out _, out var minCompletionPortThreads);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out _);


        ThreadPool.SetMinThreads(Math.Min(maxWorkerThreads, Math.Max(workerThreads, Environment.ProcessorCount)), minCompletionPortThreads);
    }


    /// <summary>
    ///     Handle the client.
    /// </summary>
    /// <param name="tcpClientSocket">The client socket.</param>
    /// <param name="endPoint">The proxy endpoint.</param>
    /// <returns>The task.</returns>
    private async Task HandleClient(Socket tcpClientSocket, ProxyEndPoint endPoint)
    {
        tcpClientSocket.ReceiveTimeout = ConnectionTimeOutSeconds * 1000;
        tcpClientSocket.SendTimeout = ConnectionTimeOutSeconds * 1000;

        tcpClientSocket.LingerState = new LingerOption(true, TcpTimeWaitSeconds);

        await InvokeClientConnectionCreateEvent(tcpClientSocket);

        using var clientConnection = new TcpClientConnection(this, tcpClientSocket);
        if (endPoint is ExplicitProxyEndPoint eep)
            await HandleClient(eep, clientConnection);
        else if (endPoint is TransparentProxyEndPoint tep)
            await HandleClient(tep, clientConnection);
        else if (endPoint is SocksProxyEndPoint sep) await HandleClient(sep, clientConnection);
    }

    /// <summary>
    ///     Handle exception.
    /// </summary>
    /// <param name="clientStream">The client stream.</param>
    /// <param name="exception">The exception.</param>
    private void OnException(HttpClientStream? clientStream, Exception exception)
    {
        ExceptionFunc?.Invoke(exception);
    }

    /// <summary>
    ///     Quit listening on the given end point.
    /// </summary>
    private static void QuitListen(ProxyEndPoint endPoint)
    {
        endPoint.Listener!.Stop();
        endPoint.Listener.Server.Dispose();
    }

    /// <summary>
    ///     Update client connection count.
    /// </summary>
    /// <param name="increment">Should we increment/decrement?</param>
    internal void UpdateClientConnectionCount(bool increment)
    {
        if (increment)
            Interlocked.Increment(ref clientConnectionCount);
        else
            Interlocked.Decrement(ref clientConnectionCount);

        try
        {
            ClientConnectionCountChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            OnException(null, ex);
        }
    }

    /// <summary>
    ///     Update server connection count.
    /// </summary>
    /// <param name="increment">Should we increment/decrement?</param>
    internal void UpdateServerConnectionCount(bool increment)
    {
        if (increment)
            Interlocked.Increment(ref serverConnectionCount);
        else
            Interlocked.Decrement(ref serverConnectionCount);

        try
        {
            ServerConnectionCountChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            OnException(null, ex);
        }
    }

    /// <summary>
    ///     Invoke client tcp connection events if subscribed by API user.
    /// </summary>
    /// <param name="clientSocket">The TcpClient object.</param>
    /// <returns></returns>
    internal async Task InvokeClientConnectionCreateEvent(Socket clientSocket)
    {
        // client connection created
        if (OnClientConnectionCreate != null)
            await OnClientConnectionCreate.InvokeAsync(this, clientSocket, ExceptionFunc);
    }

    /// <summary>
    ///     Invoke server tcp connection events if subscribed by API user.
    /// </summary>
    /// <param name="serverSocket">The Socket object.</param>
    /// <returns></returns>
    internal async Task InvokeServerConnectionCreateEvent(Socket serverSocket)
    {
        // server connection created
        if (OnServerConnectionCreate != null)
            await OnServerConnectionCreate.InvokeAsync(this, serverSocket, ExceptionFunc);
    }

    /// <summary>
    ///     Connection retry policy when using connection pool.
    /// </summary>
    private RetryPolicy<T> RetryPolicy<T>() where T : Exception
    {
        return new RetryPolicy<T>(NetworkFailureRetryAttempts, TcpConnectionFactory);
    }

    /// <summary>
    ///    Throw not supported exception, only supported in Windows.
    /// </summary>
    /// <param name="caller">Method name, automatically filled by <see cref="CallerMemberNameAttribute"/></param>
    private void ThrowNotSupportedException([CallerMemberName]string? caller = null)
    {
        var ex = new NotSupportedException(@"Setting system proxy settings are only supported in Windows.");
        logger.LogWarning(ex, "Setting system proxy settings are only supported in Windows. {caller}", caller);
        throw ex;
    }

    private bool disposed;

    /// <summary>
    /// Dispose the ProxyServer instance, and stop the proxy server if running.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;

        disposed = true;

        if (ProxyRunning)
            try
            {
                Stop();
            }
            catch
            {
                // ignore
            }

        if (disposing)
        {
            CertificateManager?.Dispose();
            BufferPool?.Dispose();
            loggerFactory.Dispose();
            activitySource?.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    ~ProxyServer()
    {
        Dispose(false);
    }
}