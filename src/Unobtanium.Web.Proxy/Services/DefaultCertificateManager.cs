using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Internal;

namespace Unobtanium.Web.Proxy.Services;

/// <summary>
/// Implementation of <see cref="ICertificateManager"/> that manages X.509 certificates for secure communication and caches them to disk and in-memory
/// </summary>
public class DefaultCertificateManager : IDisposable, ICertificateManager
{
    private readonly ILogger<DefaultCertificateManager> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly CertificateManagerConfiguration _configuration;
    private readonly AsyncConcurrentDictionary<string, X509Certificate2> cachedCertificates = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCertificateManager"/> class with optional configuration,
    /// logging, and time provider dependencies.
    /// </summary>
    /// <param name="options">The configuration options for the certificate manager. If null, a default configuration is used.</param>
    /// <param name="logger">The logger instance used for logging operations. If null, a no-op logger is used.</param>
    /// <param name="timeProvider">The time provider used for time-related operations. If null, the system time provider is used.</param>
    public DefaultCertificateManager ( IOptions<CertificateManagerConfiguration>? options = null, ILogger<DefaultCertificateManager>? logger = null, TimeProvider? timeProvider = null )
    {
        _logger = logger ?? new NullLogger<DefaultCertificateManager>();
        _configuration = options?.Value ?? new CertificateManagerConfiguration();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Get a <see cref="X509Certificate2"/> for the specified host, signed by the root certificate.
    /// </summary>
    /// <param name="host">host to get a cert for</param>
    /// <param name="cancellationToken"></param>
    /// <remarks>This will check the in-memory (and disk when asked) cache, otherwise generate and start background task to save to disk</remarks>
    /// <exception cref="ArgumentException">If invalid hostname is provided</exception>
    public async Task<X509Certificate2> GetCertificateAsync ( string host, CancellationToken cancellationToken )
    {
        ArgumentNullException.ThrowIfNullOrEmpty(host);
        if (!IsValidHostnameOrIpAddress(host))
        {
            throw new ArgumentException("The provided host is not a valid hostname or IP address.", nameof(host));
        }
        using var activity = ProxyServerDefaults.ProxyActivitySource.StartActivity(nameof(GetCertificateAsync), ActivityKind.Internal);
        activity?.SetTag("proxy.cert.host", host);

        var shouldSaveHostCertificate = false;
        // Remove strange characters from the host name for cache key and file name
        var sanitizedHost = host
            .Replace(":", "_")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace("?", "_")
            .Replace("*", "_")
            .Replace("\"", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("|", "_")
            .Replace(".", "_").Trim().ToLowerInvariant();
        var cert = await cachedCertificates.GetOrAddAsync(sanitizedHost, async ( ct ) =>
        {
            if (_configuration.CachePath is not null && _configuration.CacheHostCertificates)
            {
                var cacheFile = System.IO.Path.Combine(_configuration.CachePath, $"{sanitizedHost}.pfx");
                if (System.IO.File.Exists(cacheFile))
                {
                    _logger.LogInformation("Loading certificate for {Host} from file: {CacheFile}", sanitizedHost, cacheFile);
                    return new X509Certificate2(cacheFile, "", X509KeyStorageFlags.Exportable);
                }
            }
            shouldSaveHostCertificate = _configuration.CachePath is not null && _configuration.CacheHostCertificates;
            _logger.LogInformation("Creating new certificate for {Host}", host);
            var rootCert = await GetRootCertificateAsync(true, ct);
            var leafCert = CreateLeafCertificate(host, rootCert);
            return leafCert;
        }, cancellationToken);
        if (shouldSaveHostCertificate)
        {
            _logger.LogInformation("Certificate for {Host} created, saving to cache file.", host);
            _ = Task.Run(async () =>
            {
                try
                {
                    var cacheFile = System.IO.Path.Combine(_configuration.CachePath!, $"{sanitizedHost}.pfx");
                    _logger.LogInformation("Saving certificate for {Host} to file: {CacheFile}", host, cacheFile);
                    await SaveCertificateToPathAsync(cert, cacheFile, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save certificate for {Host} to file.", host);
                }
            }, CancellationToken.None);
        }
        return cert;
    }

    /// <summary>
    /// Asynchronously retrieves the root certificate, creating and caching it if necessary.
    /// </summary>
    /// <remarks>If a cached root certificate exists and caching is enabled, the certificate is loaded from
    /// the cache.  Otherwise, a new root certificate is created. If caching is enabled and a new certificate is
    /// created,  it is saved to the configured cache path asynchronously.</remarks>
    /// <param name="includePrivateKey">Should the certificate include it's private key?</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the root certificate as an <see
    /// cref="X509Certificate2"/> object.</returns>
    public async Task<X509Certificate2> GetRootCertificateAsync ( bool includePrivateKey, CancellationToken cancellationToken )
    {
        var shouldSaveRootCertificate = false;
        var cert = await cachedCertificates.GetOrAddAsync("root", ( ct ) =>
        {
            if (_configuration.CachePath is not null && _configuration.CacheRootCertificate)
            {
                var cacheFile = System.IO.Path.Combine(_configuration.CachePath, "root.pfx");
                if (System.IO.File.Exists(cacheFile))
                {
                    _logger.LogInformation("Loading root certificate from file: {CacheFile}", cacheFile);
                    return Task.FromResult(new X509Certificate2(cacheFile, "", X509KeyStorageFlags.Exportable));
                }
            }
            // If we reach here, we need to create a new root certificate
            // and possibly save it to the cache
            shouldSaveRootCertificate = _configuration.CachePath is not null && _configuration.CacheRootCertificate;
            _logger.LogInformation("Creating new root certificate with {RootCn}", _configuration.RootCertificateName);
            var rootCert = CreateRootCertificate(_configuration.RootCertificateName, 2048);
            return Task.FromResult(rootCert);
        }, cancellationToken);
        if (shouldSaveRootCertificate)
        {
            _logger.LogInformation("Root certificate created, saving to cache file.");
            try
            {
                var cacheFile = System.IO.Path.Combine(_configuration.CachePath!, "root.pfx");
                _logger.LogInformation("Saving root certificate to file: {CacheFile}", cacheFile);
                await SaveCertificateToPathAsync(cert, cacheFile, cancellationToken);
                cacheFile = System.IO.Path.Combine(_configuration.CachePath!, "root.crt");
                await SaveRootCertificateToPathAsync(cert, cacheFile, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save root certificate to file.");
            }
        }
        if (!includePrivateKey)
        {
            // If the caller does not want the private key, we strip it from the certificate
            _logger.LogDebug("Stripping private key from root certificate.");
            cert = CertificateUtils.StripPrivateKey(cert);
        }
        return cert;
    }

    /// <summary>
    /// Validates if the provided string is a valid hostname or IP address.
    /// </summary>
    /// <param name="host">The host string to validate.</param>
    /// <returns>True if the host is a valid hostname or IP address; otherwise, false.</returns>
    private static bool IsValidHostnameOrIpAddress ( string host )
    {
        // Check if it's a valid IP address (IPv4 or IPv6)
        if (IPAddress.TryParse(host, out _))
        {
            return true;
        }

        // Check if it's a valid hostname using Uri.CheckHostName
        var hostNameType = Uri.CheckHostName(host);
        return hostNameType == UriHostNameType.Dns || hostNameType == UriHostNameType.IPv4 || hostNameType == UriHostNameType.IPv6;
    }

    private X509Certificate2 CreateRootCertificate ( string subjectCn, int keySize = 2048 )
    {
        using var rsa = RSA.Create(keySize); // Generate a new RSA key pair

        var request = new CertificateRequest($"CN={subjectCn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Set key usage
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign, false));

        // Set basic constraints
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));

        // Set the validity period
        var notBefore = _timeProvider.GetUtcNow().AddDays(-1);
        var notAfter = _timeProvider.GetUtcNow().AddDays(_configuration.CertificateLifetimeDays);

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
        var notBefore = _timeProvider.GetUtcNow().AddDays(-1);
        var notAfter = _timeProvider.GetUtcNow().AddDays(_configuration.CertificateLifetimeDays);

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

    private async Task SaveCertificateToPathAsync ( X509Certificate2 certificate, string path, CancellationToken cancellationToken )
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        var directory = System.IO.Path.GetDirectoryName(path);
        if (directory is not null && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        await System.IO.File.WriteAllBytesAsync(path, certificate.Export(X509ContentType.Pfx), cancellationToken);
    }

    private async Task SaveRootCertificateToPathAsync ( X509Certificate2 certificate, string path, CancellationToken cancellationToken )
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        var directory = System.IO.Path.GetDirectoryName(path);
        if (directory is not null && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        await System.IO.File.WriteAllBytesAsync(path, certificate.Export(X509ContentType.Cert), cancellationToken);
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    /// <remarks>Call this method when the instance is no longer needed to free up resources.  After calling
    /// <see cref="Dispose"/>, the instance is in an unusable state and  should not be used further. Always ensure to
    /// release all references to the  instance after calling this method to allow the garbage collector to reclaim  the
    /// memory.</remarks>
    public void Dispose ()
    {
        //throw new NotImplementedException();
    }
}

/// <summary>
/// Configuration for DefaultCertificateManager.
/// </summary>
/// <remarks>Use services.Configure&lt;CertificateManagerConfiguration&gt;(....)</remarks>
public class CertificateManagerConfiguration
{
    /// <summary>
    /// Gets or sets the number of days for which a generated certificate remains valid.
    /// </summary>
    public int CertificateLifetimeDays { get; set; } = 300;

    /// <summary>
    /// Gets or sets the file system path where cached data is stored.
    /// </summary>
    /// <remarks>The specified path should be a valid directory path. If the path is <see langword="null"/>,
    /// caching functionality may be disabled or unavailable.</remarks>
    public string? CachePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the root certificate should be cached.
    /// </summary>
    public bool CacheRootCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether host certificates should be cached.
    /// </summary>
    /// <remarks>Enabling this property can improve performance by reusing cached certificates,  but may
    /// result in stale data if certificates are updated frequently.</remarks>
    public bool CacheHostCertificates { get; set; } = false;

    /// <summary>
    /// Gets or sets the name of the root certificate used for establishing trust.
    /// </summary>
    public string RootCertificateName { get; set; } = "Unobtanium Root CA";
}
