using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Titanium.Web.Proxy.Helpers;

namespace Titanium.Web.Proxy.Network;

/// <summary>
/// Provides a default disk-based cache implementation for storing certificates.
/// </summary>
public sealed class DefaultCertificateDiskCache : ICertificateCache
{
    private const string DefaultCertificateDirectoryName = "crts";
    private const string DefaultCertificateFileExtension = ".pfx";
    private const string DefaultRootCertificateFileName = "rootCert" + DefaultCertificateFileExtension;
    private string? rootCertificatePath;

    /// <summary>
    /// Loads the root certificate from the specified path or name.
    /// </summary>
    /// <param name="pathOrName">The path or name of the root certificate.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="storageFlags">The storage flags for the root certificate.</param>
    /// <returns>The loaded root certificate, or null if not found.</returns>
    public X509Certificate2? LoadRootCertificate ( string pathOrName, string password, X509KeyStorageFlags storageFlags )
    {
        var path = GetRootCertificatePath(pathOrName);
        return LoadCertificate(path, password, storageFlags);
    }

    /// <summary>
    /// Saves the root certificate to the specified path or name.
    /// </summary>
    /// <param name="pathOrName">The path or name where the root certificate will be saved.</param>
    /// <param name="password">The password for the root certificate.</param>
    /// <param name="certificate">The root certificate to save.</param>
    public void SaveRootCertificate ( string pathOrName, string password, X509Certificate2 certificate )
    {
        var path = GetRootCertificatePath(pathOrName);
        var exported = certificate.Export(X509ContentType.Pkcs12, password);
        File.WriteAllBytes(path, exported);
    }

    /// <summary>
    /// Loads a certificate from the specified subject name.
    /// </summary>
    /// <param name="subjectName">The subject name of the certificate to load.</param>
    /// <param name="storageFlags">The storage flags for the certificate.</param>
    /// <returns>The loaded certificate, or null if not found.</returns>
    public X509Certificate2? LoadCertificate ( string subjectName, X509KeyStorageFlags storageFlags )
    {
        var filePath = Path.Combine(GetCertificatePath(false), subjectName + DefaultCertificateFileExtension);
        return LoadCertificate(filePath, string.Empty, storageFlags);
    }

    /// <summary>
    /// Saves a certificate with the specified subject name.
    /// </summary>
    /// <param name="subjectName">The subject name of the certificate to save.</param>
    /// <param name="certificate">The certificate to save.</param>
    public void SaveCertificate ( string subjectName, X509Certificate2 certificate )
    {
        var filePath = Path.Combine(GetCertificatePath(true), subjectName + DefaultCertificateFileExtension);
        var exported = certificate.Export(X509ContentType.Pkcs12);
        File.WriteAllBytes(filePath, exported);
    }

    /// <summary>
    /// Clears all certificates from the cache.
    /// </summary>
    public void Clear ()
    {
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

    private static X509Certificate2? LoadCertificate ( string path, string password, X509KeyStorageFlags storageFlags )
    {
        byte[] exported;

        if (!File.Exists(path)) return null;

        try
        {
            exported = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            // file or directory not found
            return null;
        }

        return new X509Certificate2(exported, password, storageFlags);
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

#if NET6_0_OR_GREATER
                // single-file app returns string.Empty location
                if (assemblyLocation == string.Empty)
                {
                    assemblyLocation = AppContext.BaseDirectory;
                }
#endif

                var path = Path.GetDirectoryName(assemblyLocation);

                rootCertificatePath = path ?? throw new NullReferenceException();
            }
        }

        return rootCertificatePath;
    }
}
