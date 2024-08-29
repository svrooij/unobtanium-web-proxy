using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Titanium.Web.Proxy.Certificates;

/// <summary>
///     Implements certificate generation operations, in pure .NET.
/// </summary>
/// <remarks>May be the best non-BC option for .NET 6+.</remarks>
public class DotnetCertificateMaker : ICertificateGenerator
{
    private const int ValidFromDaysAdjustment = -1;
    private const int ValidToDaysAdjustmentRoot = 700;

    private readonly int ValidToDaysAdjustment;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DotnetCertificateMaker"/> class.
    /// </summary>
    /// <param name="certificateLifetime">The valid to days adjustment.</param>
    public DotnetCertificateMaker ( int certificateLifetime = 365 )
    {
        ValidToDaysAdjustment = certificateLifetime;
    }

    /// <summary>
    ///     Makes the certificate.
    /// </summary>
    /// <param name="subjectCn">The subject cn, will be set as CN and as subject alternative if the <see paramref="signingCert"/> is specified</param>
    /// <param name="signingCert">The signing cert, will generate root certificate if empty</param>
    public X509Certificate2 GenerateCertificate ( string subjectCn, X509Certificate2? signingCert )
    {
        return signingCert == null ? CreateRootCertificate(subjectCn) : CreateLeafCertificate(subjectCn, signingCert);
    }

    private static X509Certificate2 CreateRootCertificate ( string subjectCn, int keySize = 2048 )
    {
        using var rsa = RSA.Create(keySize); // Generate a new RSA key pair

        var request = new CertificateRequest($"CN={subjectCn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Set key usage
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign, false));

        // Set basic constraints
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));

        // Set the validity period
        var notBefore = DateTimeOffset.Now.AddDays(ValidFromDaysAdjustment);
        var notAfter = DateTimeOffset.Now.AddDays(ValidToDaysAdjustmentRoot);

        // Create the certificate
        var cert = request.CreateSelfSigned(notBefore, notAfter);

        // Export the certificate with the private key, then re-import it to generate an X509Certificate2 object
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), "", X509KeyStorageFlags.Exportable);
    }

    private X509Certificate2 CreateLeafCertificate ( string subjectCn, X509Certificate2 signingCert, int keySize = 2048 )
    {
        using var rsa = RSA.Create(keySize); // Generate a new RSA key pair

        var request = new CertificateRequest($"CN={subjectCn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Add subject alternative name to request
        var subjectAlternativeName = new SubjectAlternativeNameBuilder();
        if (IPAddress.TryParse(subjectCn, out var ip))
        {
            subjectAlternativeName.AddIpAddress(ip);
        }
        else
        {
            subjectAlternativeName.AddDnsName(subjectCn);
        }
        request.CertificateExtensions.Add(subjectAlternativeName.Build());

        // Set key usage
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

        // Set enhanced key usage to server authentication
        var serverAuthenticationOid = new Oid("1.3.6.1.5.5.7.3.1");
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([serverAuthenticationOid], false));

        // Set the validity period
        var notBefore = DateTimeOffset.Now.AddDays(ValidFromDaysAdjustment);
        var notAfter = DateTimeOffset.Now.AddDays(ValidToDaysAdjustment);

        if (notAfter > signingCert.NotAfter)
        {
            notAfter = signingCert.NotAfter.AddSeconds(-30);
        }
        // Generate a random serial number
        var serialNumber = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(serialNumber);
        }

        // Ensure the serial number is positive by setting the most significant bit to 0
        serialNumber[0] &= 0x7F;

        // Create the certificate and create a copy with the private key in it
        var cert = request.Create(signingCert, notBefore, notAfter, serialNumber)
            .CopyWithPrivateKey(rsa);

        return new X509Certificate2(cert.Export(X509ContentType.Pfx), "", X509KeyStorageFlags.Exportable);
    }
}
