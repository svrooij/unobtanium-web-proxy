using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy;
public interface ICertificateManager
{
    public Task<X509Certificate2> GetRootCertificateAsync ( CancellationToken cancellationToken );
    public Task<X509Certificate2> GetCertificateAsync ( string host, CancellationToken cancellationToken );
}
