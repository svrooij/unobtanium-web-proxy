using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Titanium.Web.Proxy.Network;

namespace Titanium.Web.Proxy.UnitTests
{
    [TestClass]
    public class CertificateManagerTests
    {
        private static readonly string[] hostNames
            = { "facebook.com", "youtube.com", "google.com", "bing.com", "yahoo.com" };


        [TestMethod]
        public async Task CertificateManager_EngineBouncyCastle_CreatesCertificates ()
        {
            var tasks = new List<Task>();

            using var mgr = new CertificateManager(null, null, false, false, false, null)
            {
                CertificateEngine = CertificateEngine.BouncyCastle
            };
            mgr.ClearIdleCertificates();
            await mgr.CreateRootCertificate(false, CancellationToken.None);
            for (var i = 0; i < 5; i++)
                tasks.AddRange(hostNames.Select(host => Task.Run(async () =>
                {
                    // get the connection
                    var certificate = await mgr.GetX509Certificate2Async(host, false, System.Threading.CancellationToken.None);
                    Assert.IsNotNull(certificate, $"Certificate for {host} was not generated");
                    var matches = certificate.MatchesHostname(host);
                    Assert.IsTrue(matches, $"Certificate for {host} does not match hostname");
                })));

            await Task.WhenAll(tasks.ToArray());

            mgr.StopClearIdleCertificates();
        }

        [TestMethod]
        public async Task CertificateManager_EnginePure_CreatesCertificates ()
        {
            var tasks = new List<Task>();

            using var mgr = new CertificateManager(null, null, false, false, false, null)
            {
                CertificateEngine = CertificateEngine.Pure
            };
            mgr.ClearIdleCertificates();
            await mgr.CreateRootCertificate(false, CancellationToken.None);
            for (var i = 0; i < 5; i++)
                tasks.AddRange(hostNames.Select(host => Task.Run(async () =>
                {
                    // get the connection
                    var certificate = await mgr.GetX509Certificate2Async(host, false, System.Threading.CancellationToken.None);
                    Assert.IsNotNull(certificate, $"Certificate for {host} was not generated");
                    var matches = certificate.MatchesHostname(host);
                    Assert.IsTrue(matches, $"Certificate for {host} does not match hostname");
                })));

            await Task.WhenAll(tasks.ToArray());

            mgr.StopClearIdleCertificates();
        }

        // uncomment this to compare WinCert maker performance with BC (BC takes more time for same test above)
        //[TestMethod]
        public async Task Simple_Create_Win_Certificate_Test ()
        {
            var tasks = new List<Task>();

            using var mgr = new CertificateManager(null, null, false, false, false, null)
            { CertificateEngine = CertificateEngine.DefaultWindows };

            await mgr.CreateRootCertificate(false, CancellationToken.None);
            mgr.TrustRootCertificate(true);
            mgr.ClearIdleCertificates();

            for (var i = 0; i < 5; i++)
                tasks.AddRange(hostNames.Select(host => Task.Run(async () =>
                {
                    // get the connection
                    var certificate = await mgr.GetX509Certificate2Async(host, false, System.Threading.CancellationToken.None);
                    Assert.IsNotNull(certificate, $"Certificate for {host} was not generated");
                    var matches = certificate.MatchesHostname(host);
                    Assert.IsTrue(matches, $"Certificate for {host} does not match hostname");
                })));

            await Task.WhenAll(tasks.ToArray());
            mgr.RemoveTrustedRootCertificate(true);
            mgr.StopClearIdleCertificates();
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task CertificateManager_EngineBouncyCastleFast_Creates500Certificates ()
        {
            var tasks = new List<Task>();

            using var mgr = new CertificateManager(null, null, false, false, false, null)
            { CertificateEngine = CertificateEngine.BouncyCastleFast };
            
            mgr.SaveFakeCertificates = false;
            await mgr.CreateRootCertificate(false, CancellationToken.None);
            for (var i = 0; i < 100; i++)
                tasks.AddRange(hostNames.Select(host => Task.Run(async () =>
                {
                    var certificate = await mgr.GetX509Certificate2Async(host, false, System.Threading.CancellationToken.None);
                    
                    Assert.IsNotNull(certificate, $"Certificate for {host} was not generated");
                    var matches = certificate.MatchesHostname(host);
                    Assert.IsTrue(matches, $"Certificate for {host} does not match hostname");
                })));

            await Task.WhenAll(tasks.ToArray());
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task CertificateManager_EnginePure_Creates500Certificates ()
        {
            var tasks = new List<Task>();

            using var mgr = new CertificateManager(null, null, false, false, false, null)
            { CertificateEngine = CertificateEngine.Pure };
            
            mgr.SaveFakeCertificates = false;
            await mgr.CreateRootCertificate(false, CancellationToken.None);
            for (var i = 0; i < 100; i++)
                tasks.AddRange(hostNames.Select(host => Task.Run(async () =>
                {
                    var certificate = await mgr.GetX509Certificate2Async(host, false, System.Threading.CancellationToken.None);
                    Assert.IsNotNull(certificate, $"Certificate for {host} was not generated");
                    var matches = certificate.MatchesHostname(host);
                    Assert.IsTrue(matches, $"Certificate for {host} does not match hostname");
                })));

            await Task.WhenAll(tasks.ToArray());
        }
    }
}
