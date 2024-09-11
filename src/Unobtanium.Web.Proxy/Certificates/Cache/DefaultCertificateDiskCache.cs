using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Helpers;

namespace Unobtanium.Web.Proxy.Certificates.Cache;

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
    /// <param name="certificateStorageFolder">Use a different folder to store the certificates</param>
    /// <param name="logger">You want logs right?</param>
    public DefaultCertificateDiskCache (string? certificateStorageFolder = null, ILogger? logger = null )
    {
        this.logger = logger;
        rootCertificatePath = certificateStorageFolder;
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

    /// <inheritdoc/>
    public async Task<X509Certificate2?> LoadCertificateAsync ( string subjectName, X509KeyStorageFlags storageFlags, CancellationToken cancellationToken )
    {
        Log_LoadCertificateAsyncCalled(subjectName, storageFlags);

        var filePath = Path.Combine(GetCertificatePath(false), subjectName + DefaultCertificateFileExtension);
        return await LoadCertificateAsync(filePath, string.Empty, storageFlags, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<X509Certificate2?> LoadRootCertificateAsync ( string pathOrName, string? password, X509KeyStorageFlags storageFlags, CancellationToken cancellationToken )
    {
        Log_LoadRootCertificateAsyncCalled(pathOrName, password?.Length, storageFlags);
        var path = GetRootCertificatePath(pathOrName);
        return await LoadCertificateAsync(path, password, storageFlags, cancellationToken);
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
            if (RunTime.IsUwpOnWindows || RunTime.IsWindows)
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
