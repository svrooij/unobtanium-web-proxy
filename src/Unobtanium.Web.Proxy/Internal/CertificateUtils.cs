using System;
using System.Security.Cryptography.X509Certificates;

namespace Unobtanium.Web.Proxy.Internal;
internal static class CertificateUtils
{
    public static X509Certificate2 CreateChainedCertificate ( X509Certificate2 leafCertificate, X509Certificate2 rootCertificate )
    {
        ArgumentNullException.ThrowIfNull(leafCertificate);
        ArgumentNullException.ThrowIfNull(rootCertificate);
        // Create a new certificate with the leaf certificate's private key
        var certCollection = new X509Certificate2Collection();
        certCollection.Add(leafCertificate);
        certCollection.Add(rootCertificate);
        // Export the collection as a PFX file
        var pfxData = certCollection.Export(X509ContentType.Pfx, "");
        // Create a new X509Certificate2 from the PFX data
        var chainedCertificate = new X509Certificate2(pfxData!, "", X509KeyStorageFlags.Exportable);
        // Return the chained certificate
        return chainedCertificate;
    }

    internal static X509Certificate2 StripPrivateKey ( X509Certificate2 certificate )
    {
        ArgumentNullException.ThrowIfNull(certificate);
        // Create a new certificate without the private key
        var certWithoutPrivateKey = new X509Certificate2(certificate.Export(X509ContentType.Cert));
        return certWithoutPrivateKey;
    }
}
