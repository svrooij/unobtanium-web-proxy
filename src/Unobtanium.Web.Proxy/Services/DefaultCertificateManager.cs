using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Internal;

namespace Unobtanium.Web.Proxy.Services;
public class DefaultCertificateManager : IDisposable, ICertificateManager
{
    private readonly ILogger<DefaultCertificateManager> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly CertificateManagerConfiguration _configuration;
    private readonly AsyncConcurrentDictionary<string, X509Certificate2> cachedCertificates = new();
    public DefaultCertificateManager ( IOptions<CertificateManagerConfiguration>? options = null, ILogger<DefaultCertificateManager>? logger = null, TimeProvider? timeProvider = null )
    {
        _logger = logger ?? new NullLogger<DefaultCertificateManager>();
        _configuration = options?.Value ?? new CertificateManagerConfiguration();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<X509Certificate2> GetCertificateAsync ( string host, CancellationToken cancellationToken )
    {
        using var activity = ProxyServerDefaults.ProxyActivitySource.StartActivity(nameof(GetCertificateAsync), ActivityKind.Internal);

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
            var rootCert = await GetRootCertificateAsync(ct);
            var leafCert = CreateLeafCertificate(host, rootCert);
            return leafCert;
        }, cancellationToken);
        if (shouldSaveHostCertificate)
        {
            _logger.LogInformation("Certificate for {Host} created, saving to cache file.", host);
            _ = Task.Run(async() =>
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

    public async Task<X509Certificate2> GetRootCertificateAsync ( CancellationToken cancellationToken )
    {
        var shouldSaveRootCertificate = false;
        var cert = await cachedCertificates.GetOrAddAsync("root", async ( ct ) =>
        {
            if (_configuration.CachePath is not null && _configuration.CacheRootCertificate)
            {
                var cacheFile = System.IO.Path.Combine(_configuration.CachePath, "root.pfx");
                if (System.IO.File.Exists(cacheFile))
                {
                    _logger.LogInformation("Loading root certificate from file: {CacheFile}", cacheFile);
                    return new X509Certificate2(cacheFile, "", X509KeyStorageFlags.Exportable);
                }
            }
            shouldSaveRootCertificate = _configuration.CachePath is not null && _configuration.CacheRootCertificate;
            _logger.LogInformation("Creating new root certificate with {RootCn}", _configuration.RootCertificateName);
            var rootCert = CreateRootCertificate(_configuration.RootCertificateName, 2048);
            return rootCert;
        }, cancellationToken);
        if (shouldSaveRootCertificate)
        {
            _logger.LogInformation("Root certificate created, saving to cache file.");
            _ = Task.Run(async() =>
            {
                try
                {
                    var cacheFile = System.IO.Path.Combine(_configuration.CachePath!, "root.pfx");
                    _logger.LogInformation("Saving root certificate to file: {CacheFile}", cacheFile);
                    await SaveCertificateToPathAsync(cert, cacheFile, CancellationToken.None);
                    cacheFile = System.IO.Path.Combine(_configuration.CachePath!, "root.crt");
                    await SaveRootCertificateToPathAsync(cert, cacheFile, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save root certificate to file.");
                }
            }, CancellationToken.None);
        }
        return cert;
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

    private async Task SaveCertificateToPathAsync(X509Certificate2 certificate, string path, CancellationToken cancellationToken)
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

    private async Task SaveRootCertificateToPathAsync(X509Certificate2 certificate, string path, CancellationToken cancellationToken)
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

    public void Dispose ()
    {
        //throw new NotImplementedException();
    }
}

public class CertificateManagerConfiguration
{
    public int CertificateLifetimeDays { get; set; } = 300;
    public string? CachePath { get; set; }
    public bool CacheRootCertificate { get; set; } = true;
    public bool CacheHostCertificates { get; set; } = false;

    public string RootCertificateName { get; set; } = "Unobtanium Root CA";
}
