﻿using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.Helpers;

namespace Titanium.Web.Proxy.Certificates.Cache;

/// <summary>
/// Provides a default disk-based cache implementation for storing certificates.
/// </summary>
internal sealed partial class DefaultCertificateDiskCache : ICertificateCache
{
    public readonly ILogger? logger;
    private const string DefaultCertificateDirectoryName = "crts";
    private const string DefaultCertificateFileExtension = ".pfx";
    private const string DefaultRootCertificateFileName = "rootCert" + DefaultCertificateFileExtension;
    private string? rootCertificatePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCertificateDiskCache"/> class.
    /// </summary>
    /// <param name="logger">You want logs right?</param>
    public DefaultCertificateDiskCache ( ILogger? logger = null )
    {
        this.logger = logger;
    }

    /// <summary>
    /// Clears all certificates from the cache.
    /// </summary>
    public void Clear ()
    {
        logger?.LogTrace("Clear called");

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

    /// <summary>
    /// Loads a certificate from the specified subject name.
    /// </summary>
    /// <param name="subjectName">The subject name of the certificate to load.</param>
    /// <param name="storageFlags">The storage flags for the certificate.</param>
    /// <returns>The loaded certificate, or null if not found.</returns>
    [Obsolete("Use LoadCertificateAsync instead")]
    public X509Certificate2? LoadCertificate ( string subjectName, X509KeyStorageFlags storageFlags )
    {
        logger?.LogTrace("LoadCertificate called");

        return LoadCertificateAsync(subjectName, storageFlags, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<X509Certificate2?> LoadCertificateAsync ( string subjectName, X509KeyStorageFlags storageFlags, CancellationToken cancellationToken )
    {
        Log_LoadCertificateAsyncCalled(subjectName, storageFlags);

        var filePath = Path.Combine(GetCertificatePath(false), subjectName + DefaultCertificateFileExtension);
        return await LoadCertificateAsync(filePath, string.Empty, storageFlags, cancellationToken);
    }

    /// <summary>
    /// Loads the root certificate from the specified path or name.
    /// </summary>
    /// <param name="pathOrName">The path or name of the root certificate.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="storageFlags">The storage flags for the root certificate.</param>
    /// <returns>The loaded root certificate, or null if not found.</returns>
    [Obsolete("Use LoadRootCertificateAsync instead")]
    public X509Certificate2? LoadRootCertificate ( string pathOrName, string? password, X509KeyStorageFlags storageFlags )
    {
        logger?.LogTrace("LoadRootCertificate called");
        return LoadRootCertificateAsync(pathOrName, password, storageFlags, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<X509Certificate2?> LoadRootCertificateAsync ( string pathOrName, string? password, X509KeyStorageFlags storageFlags, CancellationToken cancellationToken )
    {
        Log_LoadRootCertificateAsyncCalled(pathOrName, password?.Length, storageFlags);
        var path = GetRootCertificatePath(pathOrName);
        return await LoadCertificateAsync(path, password, storageFlags, cancellationToken);
    }

    /// <summary>
    /// Saves a certificate with the specified subject name.
    /// </summary>
    /// <param name="subjectName">The subject name of the certificate to save.</param>
    /// <param name="certificate">The certificate to save.</param>
    [Obsolete("Use SaveCertificateAsync instead")]
    public void SaveCertificate ( string subjectName, X509Certificate2 certificate )
    {
        logger?.LogTrace("SaveCertificate called");

        SaveCertificateAsync(subjectName, certificate, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task SaveCertificateAsync ( string subjectName, X509Certificate2 certificate, CancellationToken cancellationToken )
    {
        Log_SaveCertificateAsyncCalled(subjectName);

        var filePath = Path.Combine(GetCertificatePath(true), subjectName + DefaultCertificateFileExtension);
        var exported = certificate.Export(X509ContentType.Pkcs12);
        await File.WriteAllBytesAsync(filePath, exported, cancellationToken);
        Log_SaveCertificateAsyncFinished();
    }

    /// <summary>
    /// Saves the root certificate to the specified path or name.
    /// </summary>
    /// <param name="pathOrName">The path or name where the root certificate will be saved.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="certificate">The root certificate to save.</param>
    public void SaveRootCertificate ( string pathOrName, string password, X509Certificate2 certificate )
    {
        logger?.LogTrace("SaveRootCertificate called");

        SaveRootCertificateAsync(pathOrName, password, certificate, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task SaveRootCertificateAsync ( string pathOrName, string password, X509Certificate2 certificate, CancellationToken cancellationToken )
    {
        Log_SaveRootCertificateAsyncCalled(pathOrName, password?.Length);

        var path = GetRootCertificatePath(pathOrName);
        var exported = certificate.Export(X509ContentType.Pkcs12, password);
        await File.WriteAllBytesAsync(path, exported, cancellationToken);
    }

    [LoggerMessage(11, LogLevel.Information, "Certificate path {Path} does not exists")]
    internal partial void Log_CertificatePathDoesNotExists ( string Path );

    [LoggerMessage(13, LogLevel.Warning, "Failed loading certificate from {Path}")]
    internal partial void Log_FailedLoadingCertificateFromPath ( Exception e, string Path );

    [LoggerMessage(3, LogLevel.Trace, "LoadCertificateAsync(path:{PathOrName}, storageFlags: {StorageFlags}) called")]
    internal partial void Log_LoadCertificateAsyncCalled ( string PathOrName, X509KeyStorageFlags StorageFlags );

    [LoggerMessage(10, LogLevel.Trace, "LoadCertificateAsync(path: {Path}, password length: {PasswordLength}, storageFlags: {StorageFlags}) called")]
    internal partial void Log_LoadCertificateAsyncCalled ( string Path, int? PasswordLength, X509KeyStorageFlags StorageFlags );

    [LoggerMessage(12, LogLevel.Trace, "Loaded {NumberOfBytes} from {path}, trying to open cert")]
    internal partial void Log_LoadedBytesFromPath ( int? NumberOfBytes, string Path );

    [LoggerMessage(0, LogLevel.Trace, "LoadRootCertificateAsync(path:{PathOrName}, password length:{PasswordLength}, storageFlags: {StorageFlags}) called")]
    internal partial void Log_LoadRootCertificateAsyncCalled ( string PathOrName, int? PasswordLength, X509KeyStorageFlags StorageFlags );
    [LoggerMessage(4, LogLevel.Trace, "SaveCertificateAsync(subject:{Subject}) called")]
    internal partial void Log_SaveCertificateAsyncCalled ( string Subject );

    [LoggerMessage(5, LogLevel.Trace, "SaveCertificateAsync() finished")]
    internal partial void Log_SaveCertificateAsyncFinished ();

    [LoggerMessage(2, LogLevel.Trace, "SaveRootCertificateAsync(path:{PathOrName}, password length:{PasswordLength}) called")]
    internal partial void Log_SaveRootCertificateAsyncCalled ( string PathOrName, int? PasswordLength );

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

    private string GetRootCertificatePath ( string pathOrName )
    {
        if (Path.IsPathRooted(pathOrName)) return pathOrName;

        return Path.Combine(GetRootCertificateDirectory(),
            string.IsNullOrEmpty(pathOrName) ? DefaultRootCertificateFileName : pathOrName);
    }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task<X509Certificate2?> LoadCertificateAsync ( string path, string? password, X509KeyStorageFlags storageFlags, CancellationToken cancellationToken )
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        Log_LoadCertificateAsyncCalled(path, password?.Length, storageFlags);

        if (!File.Exists(path))
        {
            Log_CertificatePathDoesNotExists(path);
            return null;
        }

        try
        {
            // TODO: Make LoadCertificateAsync async! see https://github.com/svrooij/titanium-web-proxy/issues/25
            //var exported = await File.ReadAllBytesAsync(path, cancellationToken);
            var exported = File.ReadAllBytes(path);
            Log_LoadedBytesFromPath(exported?.Length, path);
            return new X509Certificate2(exported!, password, storageFlags);
        }
        catch (IOException ex)
        {
            Log_FailedLoadingCertificateFromPath(ex, path);
            // file or directory not found
            return null;
        }
        catch (Exception ex)
        {
            Log_FailedLoadingCertificateFromPath(ex, path);
            return null;
        }
    }
}
