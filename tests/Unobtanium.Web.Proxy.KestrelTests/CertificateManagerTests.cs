using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Unobtanium.Web.Proxy.Services;

namespace Unobtanium.Web.Proxy.KestrelTests;

[TestClass]
public class CertificateManagerTests
{
    private string _tempCacheDirectory = null!;

    [TestInitialize]
    public void Setup ()
    {
        // Create a temporary directory for caching tests
        _tempCacheDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempCacheDirectory);
    }

    [TestCleanup]
    public void Cleanup ()
    {
        // Clean up temporary directory
        if (Directory.Exists(_tempCacheDirectory))
        {
            Directory.Delete(_tempCacheDirectory, true);
        }
    }

    #region Constructor Tests

    [TestMethod]
    public void Constructor_WithNullOptions_ShouldUseDefaultConfiguration ()
    {
        // Arrange & Act
        using var manager = new DefaultCertificateManager(options: null);

        // Assert - Should not throw and manager should be created
        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ShouldUseNullLogger ()
    {
        // Arrange & Act
        using var manager = new DefaultCertificateManager(logger: null);

        // Assert - Should not throw and manager should be created
        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public void Constructor_WithNullTimeProvider_ShouldUseSystemTimeProvider ()
    {
        // Arrange & Act
        using var manager = new DefaultCertificateManager(timeProvider: null);

        // Assert - Should not throw and manager should be created
        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public void Constructor_WithAllNullParameters_ShouldUseDefaults ()
    {
        // Arrange & Act
        using var manager = new DefaultCertificateManager(null, null, null);

        // Assert - Should not throw and manager should be created
        Assert.IsNotNull(manager);
    }

    [TestMethod]
    public void Constructor_WithCustomConfiguration_ShouldUseProvidedConfiguration ()
    {
        // Arrange
        var config = new CertificateManagerConfiguration
        {
            CachePath = "test-path",
            CacheRootCertificate = false,
            CacheHostCertificates = true,
            CertificateLifetimeDays = 100,
            RootCertificateName = "Test Root CA"
        };
        var options = Options.Create(config);

        // Act & Assert
        using var manager = new DefaultCertificateManager(options);
        Assert.IsNotNull(manager);
    }

    #endregion

    #region GetRootCertificateAsync Tests

    [TestMethod]
    public async Task GetRootCertificateAsync_FirstCall_ShouldCreateNewRootCertificate ()
    {
        // Arrange
        var config = new CertificateManagerConfiguration
        {
            RootCertificateName = "Test Root CA",
            CertificateLifetimeDays = 365
        };
        using var manager = new DefaultCertificateManager(Options.Create(config));

        // Act
        var rootCert = await manager.GetRootCertificateAsync(true, CancellationToken.None);

        // Assert
        Assert.IsNotNull(rootCert);
        Assert.IsTrue(rootCert.Subject.Contains("Test Root CA"));
        Assert.IsTrue(rootCert.HasPrivateKey);

        // Verify it's a CA certificate (basic constraints)
        var basicConstraints = rootCert.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();
        Assert.IsNotNull(basicConstraints);
        Assert.IsTrue(basicConstraints.CertificateAuthority);
    }

    [TestMethod]
    public async Task GetRootCertificateAsync_MultipleCalls_ShouldReturnSameCachedCertificate ()
    {
        // Arrange
        using var manager = new DefaultCertificateManager();

        // Act
        var rootCert1 = await manager.GetRootCertificateAsync(true, CancellationToken.None);
        var rootCert2 = await manager.GetRootCertificateAsync(true, CancellationToken.None);

        // Assert
        Assert.IsNotNull(rootCert1);
        Assert.IsNotNull(rootCert2);
        Assert.AreEqual(rootCert1.Thumbprint, rootCert2.Thumbprint);
    }

    [TestMethod]
    [Ignore("Fighting with UTC / local time issues")]
    public async Task GetRootCertificateAsync_WithCustomTimeProvider_ShouldUseProvidedTime ()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero).ToUniversalTime();
        var timeProvider = new TestTimeProvider(fixedTime);
        var config = new CertificateManagerConfiguration { CertificateLifetimeDays = 365 };

        using var manager = new DefaultCertificateManager(Options.Create(config), timeProvider: timeProvider);

        // Act
        var rootCert = await manager.GetRootCertificateAsync(true, CancellationToken.None);

        // Assert
        Assert.IsNotNull(rootCert);

        // Certificate should be valid from yesterday (fixedTime - 1 day) to fixedTime + 365 days
        var expectedNotBefore = fixedTime.AddDays(-1);
        var expectedNotAfter = fixedTime.AddDays(365);

        // Allow some tolerance for certificate creation time
        rootCert.NotBefore.Should().BeCloseTo(expectedNotBefore.DateTime, TimeSpan.FromSeconds(5), "because the certificate should use the timeprovider to calculate the start time");
        rootCert.NotAfter.Should().BeCloseTo(expectedNotAfter.DateTime, TimeSpan.FromSeconds(5), "because the certificate should be valid for the configured lifetime");
    }

    [TestMethod]
    public async Task GetRootCertificateAsync_WithCachingEnabled_ShouldSaveAndLoadFromDisk ()
    {
        // Arrange
        var config = new CertificateManagerConfiguration
        {
            CachePath = _tempCacheDirectory,
            CacheRootCertificate = true
        };
        string? certThumbprint = null;

        // First manager to create the certificate
        using (var manager1 = new DefaultCertificateManager(Options.Create(config)))
        {
            // Act - Create and cache certificate
            var rootCert1 = await manager1.GetRootCertificateAsync(true, CancellationToken.None);
            certThumbprint = rootCert1.Thumbprint;
            // Wait a bit for the background save task to complete
            await Task.Delay(100);

            // Assert - Certificate files should exist
            var pfxFile = Path.Combine(_tempCacheDirectory, "root.pfx");
            var crtFile = Path.Combine(_tempCacheDirectory, "root.crt");
            Assert.IsTrue(File.Exists(pfxFile), "PFX file should be created");
            Assert.IsTrue(File.Exists(crtFile), "CRT file should be created");
        }

        // Second manager to load from cache
        using (var manager2 = new DefaultCertificateManager(Options.Create(config)))
        {
            // Act - Load from cache
            var rootCert2 = await manager2.GetRootCertificateAsync(true, CancellationToken.None);

            // Assert - Should be loaded from disk
            rootCert2.Should().NotBeNull("because the root certificate should be loaded from cache");
            rootCert2.Thumbprint.Should().Be(certThumbprint, "because the cached root certificate should match the original certificate");
            rootCert2.HasPrivateKey.Should().BeTrue("because the root certificate should have a private key");
        }
    }

    [TestMethod]
    public async Task GetRootCertificateAsync_WithCachingDisabled_ShouldNotSaveToDisk ()
    {
        // Arrange
        var config = new CertificateManagerConfiguration
        {
            CachePath = _tempCacheDirectory,
            CacheRootCertificate = false
        };
        using var manager = new DefaultCertificateManager(Options.Create(config));

        // Act
        var rootCert = await manager.GetRootCertificateAsync(true, CancellationToken.None);

        // Wait a bit to ensure no background save occurs
        await Task.Delay(100);
        var cacheFileExists = File.Exists(Path.Combine(_tempCacheDirectory, "root.pfx"));

        // Assert
        rootCert.Should().NotBeNull("because the root certificate should be created in memory");
        cacheFileExists.Should().BeFalse("because caching is disabled, no file should be created on disk");
    }

    [TestMethod]
    public async Task GetRootCertificateAsync_WithCancellationToken_ShouldRespectCancellation ()
    {
        // Arrange
        using var manager = new DefaultCertificateManager();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => manager.GetRootCertificateAsync(true, cts.Token));
    }

    #endregion

    #region GetCertificateAsync Tests

    [TestMethod]
    public async Task GetCertificateAsync_WithValidHostname_ShouldCreateSignedCertificate ()
    {
        // Arrange
        const string hostname = "example.com";
        using var manager = new DefaultCertificateManager();

        // Act
        var hostCert = await manager.GetCertificateAsync(hostname, CancellationToken.None);
        var rootCert = await manager.GetRootCertificateAsync(true, CancellationToken.None);

        // Assert
        Assert.IsNotNull(hostCert);
        Assert.IsTrue(hostCert.Subject.Contains(hostname));
        Assert.IsTrue(hostCert.HasPrivateKey);

        // Verify the certificate is signed by the root certificate
        Assert.AreEqual(rootCert.Subject, hostCert.Issuer);

        // Verify SAN extension contains the hostname
        var sanExtension = hostCert.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        Assert.IsNotNull(sanExtension);
    }

    [TestMethod]
    public async Task GetCertificateAsync_WithIPAddress_ShouldCreateCertificateWithIPSAN ()
    {
        // Arrange
        const string ipAddress = "192.168.1.1";
        using var manager = new DefaultCertificateManager();

        // Act
        var hostCert = await manager.GetCertificateAsync(ipAddress, CancellationToken.None);

        // Assert
        Assert.IsNotNull(hostCert);
        Assert.IsTrue(hostCert.Subject.Contains(ipAddress));

        // Verify SAN extension exists (IP addresses should be in SAN)
        var sanExtension = hostCert.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        Assert.IsNotNull(sanExtension);
    }

    [TestMethod]
    public async Task GetCertificateAsync_SameHostMultipleCalls_ShouldReturnCachedCertificate ()
    {
        // Arrange
        const string hostname = "test.example.com";
        using var manager = new DefaultCertificateManager();

        // Act
        var cert1 = await manager.GetCertificateAsync(hostname, CancellationToken.None);
        var cert2 = await manager.GetCertificateAsync(hostname, CancellationToken.None);

        // Assert
        Assert.IsNotNull(cert1);
        Assert.IsNotNull(cert2);
        Assert.AreEqual(cert1.Thumbprint, cert2.Thumbprint);
    }

    [TestMethod]
    public async Task GetCertificateAsync_WithSpecialCharacters_ShouldThrowException ()
    {
        // Arrange
        const string hostname = "test:8080/path?query=value*wild<card>pipe|quote\"backslash\\";
        using var manager = new DefaultCertificateManager();

        // Act
        var act = () => manager.GetCertificateAsync(hostname, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>("because the hostname contains invalid characters that should be sanitized");

    }

    [TestMethod]
    public async Task GetCertificateAsync_WithCachingEnabled_ShouldSaveAndLoadFromDisk ()
    {
        // Arrange
        const string hostname = "cached.example.com";
        var config = new CertificateManagerConfiguration
        {
            CachePath = _tempCacheDirectory,
            CacheHostCertificates = true,
            CacheRootCertificate = true
        };

        string? certThumbprint = null;

        // First manager to create certificate
        using (var manager1 = new DefaultCertificateManager(Options.Create(config)))
        {
            // Act
            var cert1 = await manager1.GetCertificateAsync(hostname, CancellationToken.None);
            certThumbprint = cert1.Thumbprint;

            // Wait for background save
            await Task.Delay(100);

            // Assert - Cache file should exist
            var sanitizedHost = hostname.Replace(".", "_").ToLowerInvariant();
            var cacheFile = Path.Combine(_tempCacheDirectory, $"{sanitizedHost}.pfx");
            Assert.IsTrue(File.Exists(cacheFile));
        }

        // Second manager to load from cache
        using (var manager2 = new DefaultCertificateManager(Options.Create(config)))
        {
            // Act
            var cert2 = await manager2.GetCertificateAsync(hostname, CancellationToken.None);

            // Assert
            cert2.Should().NotBeNull("because the certificate should be loaded from cache");
            cert2.Thumbprint.Should().Be(certThumbprint, "because the cached certificate should match the original certificate");
        }
    }

    [TestMethod]
    public async Task GetCertificateAsync_WithCachingDisabled_ShouldNotSaveToDisk ()
    {
        // Arrange
        const string hostname = "nocache.example.com";
        var config = new CertificateManagerConfiguration
        {
            CachePath = _tempCacheDirectory,
            CacheHostCertificates = false
        };
        using var manager = new DefaultCertificateManager(Options.Create(config));

        // Act
        var cert = await manager.GetCertificateAsync(hostname, CancellationToken.None);

        // Wait to ensure no background save occurs
        await Task.Delay(100);

        // Assert
        Assert.IsNotNull(cert);
        var sanitizedHost = hostname.Replace(".", "_").ToLowerInvariant();
        var cacheFile = Path.Combine(_tempCacheDirectory, $"{sanitizedHost}.pfx");
        Assert.IsFalse(File.Exists(cacheFile));
    }

    [TestMethod]
    [Ignore("Fighting with UTC / local time issues")]
    public async Task GetCertificateAsync_WithCustomTimeProvider_ShouldRespectCertificateLifetime ()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero).ToUniversalTime();
        var timeProvider = new TestTimeProvider(fixedTime);
        var config = new CertificateManagerConfiguration { CertificateLifetimeDays = 30 };

        using var manager = new DefaultCertificateManager(Options.Create(config), timeProvider: timeProvider);
        var rootCert = await manager.GetRootCertificateAsync(true, CancellationToken.None);

        // Act
        var hostCert = await manager.GetCertificateAsync("test.com", CancellationToken.None);

        // Assert
        Assert.IsNotNull(hostCert);

        var expectedNotBefore = fixedTime.AddDays(-1);
        var expectedNotAfter = fixedTime.AddDays(30);

        hostCert.NotBefore.Should().BeCloseTo(expectedNotBefore.DateTime, TimeSpan.FromSeconds(10), "because the certificate should use the timeprovider to calculate the start time");
        hostCert.NotAfter.Should().BeCloseTo(expectedNotAfter.DateTime, TimeSpan.FromMinutes(1), "because the certificate should be valid until for the asked lifetime ");
    }

    [TestMethod]
    public async Task GetCertificateAsync_LeafCertificateExpirationBeyondRoot_ShouldNotExceedRootExpiration ()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero).ToUniversalTime();
        var timeProvider = new TestTimeProvider(fixedTime);
        var config = new CertificateManagerConfiguration
        {
            CertificateLifetimeDays = 10 // Root cert will have 10 days
        };

        using var manager = new DefaultCertificateManager(Options.Create(config), timeProvider: timeProvider);

        // Get root certificate first
        var rootCert = await manager.GetRootCertificateAsync(true, CancellationToken.None);

        // Move time forward and try to create leaf certificate with longer lifetime
        timeProvider.SetCurrentTime(fixedTime.AddDays(5));
        config.CertificateLifetimeDays = 20; // Leaf would normally last 20 days, but root expires in 5

        // Act
        var leafCert = await manager.GetCertificateAsync("test.com", CancellationToken.None);

        // Assert
        leafCert.Should().NotBeNull("because the leaf certificate should be created successfully even with a shorter lifetime than the root");
        leafCert.NotAfter.Should().BeCloseTo(rootCert.NotAfter, TimeSpan.FromSeconds(31), "because the leaf certificate should not expire after the root certificate");

    }

    [TestMethod]
    public async Task GetCertificateAsync_WithCancellationToken_ShouldRespectCancellation ()
    {
        // Arrange
        using var manager = new DefaultCertificateManager();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => manager.GetCertificateAsync("test.com", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>("because the operation was cancelled before completion");
    }

    #endregion

    #region Certificate Validation Tests

    [TestMethod]
    public async Task CreatedCertificates_ShouldHaveCorrectKeyUsageExtensions ()
    {
        // Arrange
        using var manager = new DefaultCertificateManager();

        // Act
        var rootCert = await manager.GetRootCertificateAsync(true, CancellationToken.None);
        var leafCert = await manager.GetCertificateAsync("test.com", CancellationToken.None);

        // Assert - Root certificate should have CA key usage
        var rootKeyUsage = rootCert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        Assert.IsNotNull(rootKeyUsage);
        Assert.IsTrue(rootKeyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign));
        Assert.IsTrue(rootKeyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.CrlSign));

        // Assert - Leaf certificate should have digital signature and key encipherment
        var leafKeyUsage = leafCert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        Assert.IsNotNull(leafKeyUsage);
        Assert.IsTrue(leafKeyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature));
        Assert.IsTrue(leafKeyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment));

        // Assert - Leaf certificate should have server authentication EKU
        var leafEku = leafCert.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        Assert.IsNotNull(leafEku);
        Assert.IsTrue(leafEku.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.1"));
    }

    [TestMethod]
    public async Task CreatedCertificates_ShouldHaveUniqueSerialNumbers ()
    {
        // Arrange
        using var manager = new DefaultCertificateManager();

        // Act
        var cert1 = await manager.GetCertificateAsync("test1.com", CancellationToken.None);
        var cert2 = await manager.GetCertificateAsync("test2.com", CancellationToken.None);

        // Assert
        Assert.AreNotEqual(cert1.SerialNumber, cert2.SerialNumber);
    }

    #endregion

    #region Dispose Tests

    [TestMethod]
    public void Dispose_ShouldNotThrowException ()
    {
        // Arrange
        var manager = new DefaultCertificateManager();

        // Act & Assert
        manager.Dispose(); // Should not throw
    }

    [TestMethod]
    public void Dispose_CalledMultipleTimes_ShouldNotThrowException ()
    {
        // Arrange
        var manager = new DefaultCertificateManager();

        // Act & Assert
        manager.Dispose(); // First call
        manager.Dispose(); // Second call - should not throw
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task GetCertificateAsync_WithInvalidCacheDirectory_ShouldStillWork ()
    {
        // Arrange
        var config = new CertificateManagerConfiguration
        {
            CachePath = "/invalid/path/that/does/not/exist",
            CacheHostCertificates = true
        };
        using var manager = new DefaultCertificateManager(Options.Create(config));

        // Act & Assert - Should not throw, caching will fail but certificate creation should work
        var cert = await manager.GetCertificateAsync("test.com", CancellationToken.None);
        Assert.IsNotNull(cert);
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Test implementation of TimeProvider for controlled time-based testing
    /// </summary>
    private class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _currentTime;

        public TestTimeProvider ( DateTimeOffset initialTime )
        {
            _currentTime = initialTime;
        }

        public void SetCurrentTime ( DateTimeOffset time )
        {
            _currentTime = time;
        }

        public override DateTimeOffset GetUtcNow () => _currentTime;
    }

    #endregion
}
