using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Titanium.Web.Proxy.Certificates.Cache;

/// <summary>
/// Compatibility extension methods for the <see cref="ICertificateCache"/> interface.
/// </summary>
[Obsolete("Use the async methods directly instead.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ICertificateCacheExtensions
{
    /// <summary>
    /// Loads a certificate from the storage.
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="subjectName">The subject name of the certificate to load.</param>
    /// <param name="storageFlags">The storage flags for the certificate.</param>
    /// <returns>The loaded certificate, or null if not found.</returns>
    [Obsolete("Use LoadCertificateAsync instead")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static X509Certificate2? LoadCertificate ( this ICertificateCache cache, string subjectName, X509KeyStorageFlags storageFlags )
    {
        return cache.LoadCertificateAsync(subjectName, storageFlags, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Loads the root certificate from the storage.
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="pathOrName">The path or name of the root certificate.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="storageFlags">The storage flags for the root certificate.</param>
    /// <returns>The loaded root certificate, or null if not found.</returns>
    [Obsolete("Use LoadRootCertificateAsync instead")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static X509Certificate2? LoadRootCertificate ( this ICertificateCache cache, string pathOrName, string password, X509KeyStorageFlags storageFlags )
    {
        return cache.LoadRootCertificateAsync(pathOrName, password, storageFlags, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Saves a certificate to the storage.
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="subjectName">The subject name of the certificate to save.</param>
    /// <param name="certificate">The certificate to save.</param>
    [Obsolete("Use SaveCertificateAsync instead")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SaveCertificate ( this ICertificateCache cache, string subjectName, X509Certificate2 certificate )
    {
        cache.SaveCertificateAsync(subjectName, certificate, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Saves the root certificate to the storage.
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="pathOrName">The path or name where the root certificate will be saved.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="certificate">The root certificate to save.</param>
    [Obsolete("Use SaveRootCertificateAsync instead")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SaveRootCertificate ( this ICertificateCache cache, string pathOrName, string password, X509Certificate2 certificate )
    {
        cache.SaveRootCertificateAsync(pathOrName, password, certificate, CancellationToken.None).GetAwaiter().GetResult();
    }
}
