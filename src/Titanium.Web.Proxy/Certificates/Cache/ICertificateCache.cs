using System.Security.Cryptography.X509Certificates;

namespace Titanium.Web.Proxy.Network;

/// <summary>
/// Interface for certificate cache management.
/// </summary>
public interface ICertificateCache
{
    /// <summary>
    /// Loads the root certificate from the storage.
    /// </summary>
    /// <param name="pathOrName">The path or name of the root certificate.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="storageFlags">The storage flags for the root certificate.</param>
    /// <returns>The loaded root certificate, or null if not found.</returns>
    X509Certificate2? LoadRootCertificate ( string pathOrName, string password, X509KeyStorageFlags storageFlags );

    /// <summary>
    /// Saves the root certificate to the storage.
    /// </summary>
    /// <param name="pathOrName">The path or name where the root certificate will be saved.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="certificate">The root certificate to save.</param>
    void SaveRootCertificate ( string pathOrName, string password, X509Certificate2 certificate );

    /// <summary>
    /// Loads a certificate from the storage.
    /// </summary>
    /// <param name="subjectName">The subject name of the certificate to load.</param>
    /// <param name="storageFlags">The storage flags for the certificate.</param>
    /// <returns>The loaded certificate, or null if not found.</returns>
    X509Certificate2? LoadCertificate ( string subjectName, X509KeyStorageFlags storageFlags );

    /// <summary>
    /// Saves a certificate to the storage.
    /// </summary>
    /// <param name="subjectName">The subject name of the certificate to save.</param>
    /// <param name="certificate">The certificate to save.</param>
    void SaveCertificate ( string subjectName, X509Certificate2 certificate );

    /// <summary>
    /// Clears all certificates from the storage.
    /// </summary>
    void Clear ();
}
