﻿using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.Helpers;

namespace Titanium.Web.Proxy.Network;

/// <summary>
/// Provides a default disk-based cache implementation for storing certificates.
/// </summary>
internal sealed class DefaultCertificateDiskCache : ICertificateCache
{
    private const string DefaultCertificateDirectoryName = "crts";
    private const string DefaultCertificateFileExtension = ".pfx";
    private const string DefaultRootCertificateFileName = "rootCert" + DefaultCertificateFileExtension;
    public readonly ILogger? logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCertificateDiskCache"/> class.
    /// </summary>
    /// <param name="logger"></param>
    public DefaultCertificateDiskCache ( ILogger? logger = null )
    {
        this.logger = logger;
    }

    private string? rootCertificatePath;

    /// <summary>
    /// Loads the root certificate from the specified path or name.
    /// </summary>
    /// <param name="pathOrName">The path or name of the root certificate.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="storageFlags">The storage flags for the root certificate.</param>
    /// <returns>The loaded root certificate, or null if not found.</returns>
    public X509Certificate2? LoadRootCertificate ( string pathOrName, string? password, X509KeyStorageFlags storageFlags )
    {
        logger?.LogTrace("DefaultCertificateDiskCache->LoadRootCertificate called");
        return LoadRootCertificateAsync(pathOrName, password, storageFlags, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<X509Certificate2?> LoadRootCertificateAsync ( string pathOrName, string? password, X509KeyStorageFlags storageFlags, CancellationToken cancellationToken )
    {
        logger?.LogTrace("DefaultCertificateDiskCache->LoadRootCertificateAsync(pathOrName:{PathOrName}, password:{Passord}, storageFlags: {StorageFlags}) called", pathOrName, password?.Length, storageFlags);

        var path = GetRootCertificatePath(pathOrName);
        return await LoadCertificateAsync(path, password, storageFlags, cancellationToken);
    }

    /// <summary>
    /// Saves the root certificate to the specified path or name.
    /// </summary>
    /// <param name="pathOrName">The path or name where the root certificate will be saved.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="certificate">The root certificate to save.</param>
    public void SaveRootCertificate ( string pathOrName, string password, X509Certificate2 certificate )
    {
        logger?.LogTrace("DefaultCertificateDiskCache->SaveRootCertificate called");

        SaveRootCertificateAsync(pathOrName, password, certificate, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task SaveRootCertificateAsync ( string pathOrName, string password, X509Certificate2 certificate, CancellationToken cancellationToken )
    {
        logger?.LogTrace("DefaultCertificateDiskCache->SaveRootCertificateAsync called");

        var path = GetRootCertificatePath(pathOrName);
        var exported = certificate.Export(X509ContentType.Pkcs12, password);
        await File.WriteAllBytesAsync(path, exported, cancellationToken);
    }

    /// <summary>
    /// Loads a certificate from the specified subject name.
    /// </summary>
    /// <param name="subjectName">The subject name of the certificate to load.</param>
    /// <param name="storageFlags">The storage flags for the certificate.</param>
    /// <returns>The loaded certificate, or null if not found.</returns>
    public X509Certificate2? LoadCertificate ( string subjectName, X509KeyStorageFlags storageFlags )
    {
        logger?.LogTrace("DefaultCertificateDiskCache->LoadCertificate called");

        return LoadCertificateAsync(subjectName, storageFlags, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<X509Certificate2?> LoadCertificateAsync ( string subjectName, X509KeyStorageFlags storageFlags, CancellationToken cancellationToken )
    {
        logger?.LogTrace("DefaultCertificateDiskCache->LoadCertificateAsync called");

        var filePath = Path.Combine(GetCertificatePath(false), subjectName + DefaultCertificateFileExtension);
        return await LoadCertificateAsync(filePath, string.Empty, storageFlags, cancellationToken);
    }

    /// <summary>
    /// Saves a certificate with the specified subject name.
    /// </summary>
    /// <param name="subjectName">The subject name of the certificate to save.</param>
    /// <param name="certificate">The certificate to save.</param>
    public void SaveCertificate ( string subjectName, X509Certificate2 certificate )
    {
        logger?.LogTrace("DefaultCertificateDiskCache->SaveCertificate called");

        SaveCertificateAsync(subjectName, certificate, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task SaveCertificateAsync ( string subjectName, X509Certificate2 certificate, CancellationToken cancellationToken )
    {
        logger?.LogTrace("DefaultCertificateDiskCache->SaveCertificateAsync called");

        var filePath = Path.Combine(GetCertificatePath(true), subjectName + DefaultCertificateFileExtension);
        var exported = certificate.Export(X509ContentType.Pkcs12);
        await File.WriteAllBytesAsync(filePath, exported, cancellationToken);
        logger?.LogTrace("DefaultCertificateDiskCache->SaveCertificateAsync finished");

    }

    /// <summary>
    /// Clears all certificates from the cache.
    /// </summary>
    public void Clear ()
    {
        logger?.LogTrace("DefaultCertificateDiskCache->Clear called");

        try
        {
            var path = GetCertificatePath(false);
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (Exception)
        {
            // do nothing
        }
    }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task<X509Certificate2?> LoadCertificateAsync ( string path, string? password, X509KeyStorageFlags storageFlags, CancellationToken cancellationToken )
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        logger?.LogTrace("DefaultCertificateDiskCache->LoadCertificateAsync(path:{PathOrName}, password:{Passord}, storageFlags: {StorageFlags}) called", path, password?.Length, storageFlags);

        if (!File.Exists(path))
        {
            logger?.LogInformation("DefaultCertificateDiskCache->LoadCertificateAsync Certificate path {path} does not exists", path);
            return null;
        }

        try
        {
            // TODO: Make LoadCertificateAsync async! see https://github.com/svrooij/titanium-web-proxy/issues/25
            //var exported = await File.ReadAllBytesAsync(path, cancellationToken);
            var exported = File.ReadAllBytes(path);
            logger?.LogTrace("Loaded {NumberOfBytes} from {path}, trying to open cert", exported.Length, path);
            return new X509Certificate2(exported, password, storageFlags);
        }
        catch (IOException ex)
        {
            logger?.LogWarning(ex, "Error loading certificate");
            // file or directory not found
            return null;
        }
        catch (Exception e)
        {
            logger?.LogWarning(e, "Failed to load certificate from {path}", path);
            return null;
        }
    }

    private string GetRootCertificatePath ( string pathOrName )
    {
        if (Path.IsPathRooted(pathOrName)) return pathOrName;

        return Path.Combine(GetRootCertificateDirectory(),
            string.IsNullOrEmpty(pathOrName) ? DefaultRootCertificateFileName : pathOrName);
    }

    private string GetCertificatePath ( bool create )
    {
        var path = GetRootCertificateDirectory();

        var certPath = Path.Combine(path, DefaultCertificateDirectoryName);
        if (create && !Directory.Exists(certPath)) Directory.CreateDirectory(certPath);

        return certPath;
    }

    private string GetRootCertificateDirectory ()
    {
        if (rootCertificatePath == null)
        {
            if (RunTime.IsUwpOnWindows)
            {
                rootCertificatePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            else if (RunTime.IsLinux)
            {
                rootCertificatePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            else if (RunTime.IsMac)
            {
                rootCertificatePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            else
            {
                var assemblyLocation = GetType().Assembly.Location;

                // dynamically loaded assemblies returns string.Empty location
                if (assemblyLocation == string.Empty) assemblyLocation = Assembly.GetEntryAssembly()!.Location;

                // single-file app returns string.Empty location
                if (assemblyLocation == string.Empty)
                {
                    assemblyLocation = AppContext.BaseDirectory;
                }

                var path = Path.GetDirectoryName(assemblyLocation);

                rootCertificatePath = path ?? throw new InvalidOperationException("Certificate cache path could not be determind");
            }
        }

        return rootCertificatePath;
    }
}
