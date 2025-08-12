using System.Security.Cryptography.X509Certificates;

namespace Unobtanium.Proxy.Interfaces;
public interface ICertificateManager
{
    public Task<X509Certificate2> GetRootCertificateAsync ( CancellationToken cancellationToken );
    public Task<X509Certificate2> GetCertificateAsync ( string host, CancellationToken cancellationToken );
}
