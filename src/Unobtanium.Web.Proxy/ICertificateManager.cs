using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy;
/// <summary>
/// Provides methods for managing and retrieving X.509 certificates, including root certificates and host-specific
/// certificates.
/// </summary>
/// <remarks>This interface is designed to support scenarios where certificates are required for secure
/// communication, such as establishing TLS connections. Implementations may retrieve certificates from a certificate
/// store, generate them dynamically, or fetch them from an external source.</remarks>
public interface ICertificateManager
{
    /// <summary>
    /// Asynchronously retrieves the root certificate used for secure communications.
    /// </summary>
    /// <remarks>The returned certificate can be used to establish trust for secure communication channels.
    /// Ensure proper handling of the certificate to maintain security.</remarks>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Passing a canceled token will result in the task being canceled.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the root certificate as an <see
    /// cref="X509Certificate2"/> object.</returns>
    public Task<X509Certificate2> GetRootCertificateAsync ( CancellationToken cancellationToken );

    /// <summary>
    /// Asynchronously retrieves an X.509 certificate for the specified host.
    /// </summary>
    /// <remarks>This method performs an asynchronous operation to retrieve the certificate. Ensure that the 
    /// <paramref name="cancellationToken"/> is properly handled to avoid unintentional cancellation.</remarks>
    /// <param name="host">The host name for which the certificate is being requested. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the  <see cref="X509Certificate2"/>
    /// associated with the specified host.</returns>
    public Task<X509Certificate2> GetCertificateAsync ( string host, CancellationToken cancellationToken );
}
