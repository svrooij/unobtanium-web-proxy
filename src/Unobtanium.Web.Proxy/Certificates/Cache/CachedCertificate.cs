using System;
using System.Security.Cryptography.X509Certificates;

namespace Titanium.Web.Proxy.Certificates.Cache;

/// <summary>
/// An object that holds the cached certificate.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CachedCertificate"/> class.
/// </remarks>
/// <param name="certificate">The certificate to cache.</param>
internal sealed class CachedCertificate ( X509Certificate2 certificate )
{
    /// <summary>
    /// Gets the cached certificate.
    /// </summary>
    internal X509Certificate2 Certificate { get; } = certificate;

    /// <summary>
    /// Gets or sets the last time this certificate was used.
    /// Useful in determining its cache lifetime.
    /// </summary>
    internal DateTime LastAccess { get; set; }
}
