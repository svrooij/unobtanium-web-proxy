using System.Security.Cryptography.X509Certificates;

namespace Unobtanium.Web.Proxy.Certificates;

/// <summary>
///     Abstract interface for different Certificate generators
/// </summary>
internal interface ICertificateGenerator
{
    X509Certificate2 GenerateCertificate ( string subjectCn, X509Certificate2? signingCert );
}
