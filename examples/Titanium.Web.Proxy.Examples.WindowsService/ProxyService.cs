using System;
using System.Diagnostics;
using System.Net;
using System.ServiceProcess;
using System.Threading;
using Unobtanium.Web.Proxy;
using Unobtanium.Web.Proxy.Exceptions;
using Unobtanium.Web.Proxy.Models;
using WindowsServiceExample.Properties;

namespace WindowsServiceExample
{
    internal partial class ProxyService : ServiceBase
    {
        private ProxyServer _proxyServerInstance;

        public ProxyService ()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += UnhandledDomainException;
        }

        protected override void OnStart ( string[] args )
        {
            // we do all this in here so we can reload settings with a simple restart

            _proxyServerInstance = new ProxyServer(configuration: new ProxyServerConfiguration
            {
                CheckCertificateRevocation = Settings.Default.CheckCertificateRevocation,
                ConnectionTimeOutSeconds = Settings.Default.ConnectionTimeOutSeconds,
                Enable100ContinueBehaviour = Settings.Default.Enable100ContinueBehaviour,
                EnableConnectionPool = Settings.Default.EnableConnectionPool,
                EnableTcpServerConnectionPrefetch = Settings.Default.EnableTcpServerConnectionPrefetch,
                EnableWinAuth = Settings.Default.EnableWinAuth,
                ForwardToUpstreamGateway = Settings.Default.ForwardToUpstreamGateway,
                MaxCachedConnections = Settings.Default.MaxCachedConnections,
                ReuseSocket = Settings.Default.ReuseSocket,
                TcpTimeWaitSeconds = Settings.Default.TcpTimeWaitSeconds,
                EnableHttp2 = Settings.Default.EnableHttp2,
                NoDelay = Settings.Default.NoDelay,

            });

            if (Settings.Default.ListeningPort <= 0 ||
                Settings.Default.ListeningPort > 65535)
                throw new Exception("Invalid listening port");

            _proxyServerInstance.CertificateManager.SaveFakeCertificates = Settings.Default.SaveFakeCertificates;

            if (Settings.Default.ThreadPoolWorkerThreads < 0)
                _proxyServerInstance.ThreadPoolWorkerThread = Environment.ProcessorCount;
            else
                _proxyServerInstance.ThreadPoolWorkerThread = Settings.Default.ThreadPoolWorkerThreads;

            if (Settings.Default.ThreadPoolWorkerThreads < Environment.ProcessorCount)
                ProxyServiceEventLog.WriteEntry(
                    $"Worker thread count of {Settings.Default.ThreadPoolWorkerThreads} is below the " +
                    $"processor count of {Environment.ProcessorCount}. This may be on purpose.",
                    EventLogEntryType.Warning);

            var explicitEndPointV4 = new ExplicitProxyEndPoint(IPAddress.Any, Settings.Default.ListeningPort,
                Settings.Default.DecryptSsl);

            _proxyServerInstance.AddEndPoint(explicitEndPointV4);

            if (Settings.Default.EnableIpV6)
            {
                var explicitEndPointV6 = new ExplicitProxyEndPoint(IPAddress.IPv6Any, Settings.Default.ListeningPort,
                    Settings.Default.DecryptSsl);

                _proxyServerInstance.AddEndPoint(explicitEndPointV6);
            }

            if (Settings.Default.LogErrors)
                _proxyServerInstance.ExceptionFunc = ProxyException;

            _proxyServerInstance.StartAsync(cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

            ProxyServiceEventLog.WriteEntry($"Service Listening on port {Settings.Default.ListeningPort}",
                EventLogEntryType.Information);
        }

        protected override void OnStop ()
        {
            _proxyServerInstance.Stop();

            // clean up here since we make a new instance when starting
            _proxyServerInstance.Dispose();
        }

        private void ProxyException ( Exception exception )
        {
            string message;
            if (exception is ProxyHttpException pEx)
                message =
                    $"Unhandled Proxy Exception in ProxyServer, UserData = {pEx.Session?.UserData}, URL = {pEx.Session?.HttpClient.Request.RequestUri} Exception = {pEx}";
            else
                message = $"Unhandled Exception in ProxyServer, Exception = {exception}";

            ProxyServiceEventLog.WriteEntry(message, EventLogEntryType.Error);
        }

        private void UnhandledDomainException ( object sender, UnhandledExceptionEventArgs e )
        {
            ProxyServiceEventLog.WriteEntry($"Unhandled Exception in AppDomain, Exception = {e}",
                EventLogEntryType.Error);
        }
    }
}
