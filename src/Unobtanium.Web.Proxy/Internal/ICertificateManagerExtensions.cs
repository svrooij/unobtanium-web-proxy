using System;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Services;

namespace Unobtanium.Web.Proxy.Internal;
internal static class ICertificateManagerExtensions
{
    internal static async Task<X509Certificate2> GetCertificateChainAsync ( this ICertificateManager certificateManager, string host, CancellationToken cancellationToken )
    {
        ArgumentNullException.ThrowIfNull(certificateManager);
        ArgumentNullException.ThrowIfNullOrEmpty(host);
        var leafCert = await certificateManager.GetCertificateAsync(host, cancellationToken).ConfigureAwait(false);
        var rootCert = await certificateManager.GetRootCertificateAsync(false, cancellationToken).ConfigureAwait(false);
        return CertificateUtils.CreateChainedCertificate(leafCert, rootCert);
    }

    internal static async ValueTask<System.Net.Security.SslServerAuthenticationOptions> GetSslServerAuthenticationOptionsAsync (
        this ICertificateManager certificateManager, string host, CancellationToken cancellationToken )
    {
        ArgumentNullException.ThrowIfNull(certificateManager);
        ArgumentNullException.ThrowIfNullOrEmpty(host);
        var cert = await certificateManager.GetCertificateAsync(host, cancellationToken).ConfigureAwait(false);
        var rootCert = await certificateManager.GetRootCertificateAsync(false, cancellationToken).ConfigureAwait(false);
        //var policy = new X509ChainPolicy
        //{
        //    TrustMode = X509ChainTrustMode.CustomRootTrust,
        //    DisableCertificateDownloads = true,
        //    RevocationMode = X509RevocationMode.NoCheck,
        //    VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority | X509VerificationFlags.IgnoreNotTimeValid
        //};
        //policy.CustomTrustStore.Add(rootCert);
        //var combined = CertificateCombiner.CreateChainedCertificate(cert, rootCert);
        var rootCollection = new X509Certificate2Collection(rootCert);
        return new System.Net.Security.SslServerAuthenticationOptions
        {
            // Do not combine the ServerCertificate or the ServerCertificateSelectionCallback and the ServerCertificateContext, as it can cause issues with SslStream
            //ServerCertificate = combined,
            //ServerCertificateSelectionCallback = ( sender, hostName ) => combined,
            ServerCertificateContext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? SslStreamCertificateContext.Create(cert, null, offline: true, SslCertificateTrust.CreateForX509Collection(rootCollection, false))
                : SslStreamCertificateContext.Create(cert, rootCollection, offline: true, SslCertificateTrust.CreateForX509Collection(rootCollection, false)),
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.None, // Maybe force some protocol here
            ClientCertificateRequired = false,
            //CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            //RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            //{
            //    return true;
            //},
            //CertificateChainPolicy = policy
        };
    }
}
