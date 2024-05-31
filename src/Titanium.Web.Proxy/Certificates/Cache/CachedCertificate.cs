using System;
using System.Security.Cryptography.X509Certificates;

namespace Titanium.Web.Proxy.Network;

/// <summary>
/// An object that holds the cached certificate.
/// </summary>
internal sealed class CachedCertificate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CachedCertificate"/> class.
    /// </summary>
    /// <param name="certificate">The certificate to cache.</param>
    public CachedCertificate(X509Certificate2 certificate)
    {
        Certificate = certificate;
    }

    /// <summary>
    /// Gets the cached certificate.
    /// </summary>
    internal X509Certificate2 Certificate { get; }

    /// <summary>
    /// Gets or sets the last time this certificate was used.
    /// Useful in determining its cache lifetime.
    /// </summary>
    internal DateTime LastAccess { get; set; }
}
