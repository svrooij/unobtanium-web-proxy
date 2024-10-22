﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Unobtanium.Web.Proxy.Certificates.Cache;
using Unobtanium.Web.Proxy.Collections;
using Unobtanium.Web.Proxy.Helpers;
using Unobtanium.Web.Proxy.Shared;

namespace Unobtanium.Web.Proxy.Certificates;

/// <summary>
/// Certificate Engine option.
/// </summary>
public enum CertificateEngine
{
    /// <summary>
    /// Uses BouncyCastle 3rd party library.
    /// Default.
    /// </summary>
    [Obsolete("BouncyCastle will be removed soon")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    BouncyCastle = 0,

    /// <summary>
    /// Uses BouncyCastle 3rd party library.
    /// Observed to be faster than BouncyCastle.
    /// </summary>
    [Obsolete("BouncyCastle will be removed soon")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    BouncyCastleFast = 2,

    /// <summary>
    /// Uses Windows Certification Generation API and only valid in Windows OS.
    /// Observed to be faster than BouncyCastle.
    /// Bug #468 Reported.
    /// </summary>
    [Obsolete("Windows Certificate Maker is not recommended")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    DefaultWindows = 1,

    /// <summary>
    /// Uses System.Security.Cryptography.X509Certificates to generate certificates.
    /// </summary>
    /// <remarks>No external library needed.</remarks>
    Pure = 3,
}

/// <summary>
/// A class to manage SSL certificates used by this proxy server.
/// </summary>
public sealed class CertificateManager : IDisposable
{
    private const string DefaultRootCertificateIssuer = "Titanium";

    private const string DefaultRootRootCertificateName = "Titanium Root Certificate Authority";

    private readonly SemaphoreSlim rootCertCreationLock = new(1, 1);

    /// <summary>
    /// Cache dictionary
    /// </summary>
    private readonly AsyncConcurrentDictionary<string, CachedCertificate> cachedCertificates = new();

    private readonly CancellationTokenSource clearCertificatesTokenSource = new();

    private readonly ILoggerFactory loggerFactory;

    private readonly ILogger<CertificateManager> logger;

    private ICertificateGenerator? certEngineValue;

    private ICertificateCache certificateCache;

    private bool disposed;

    private CertificateEngine engine;

    private string? issuer;

    private X509Certificate2? rootCertificate;

    private string? rootCertificateName;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateManager" /> class.
    /// </summary>
    /// <param name="rootCertificateName"></param>
    /// <param name="rootCertificateIssuerName"></param>
    /// <param name="userTrustRootCertificate">
    /// Should fake HTTPS certificate be trusted by this machine's user certificate
    /// store?
    /// </param>
    /// <param name="machineTrustRootCertificate">Should fake HTTPS certificate be trusted by this machine's certificate store?</param>
    /// <param name="trustRootCertificateAsAdmin">
    /// Should we attempt to trust certificates with elevated permissions by
    /// prompting for UAC if required?
    /// </param>
    /// <param name="loggerFactory">Set <see cref="ILoggerProvider"/>, to get log messages from the certmanager, and child processes</param>
    internal CertificateManager ( string? rootCertificateName, string? rootCertificateIssuerName,
        bool userTrustRootCertificate, bool machineTrustRootCertificate, bool trustRootCertificateAsAdmin,
         ILoggerFactory? loggerFactory = null )
    {
        UserTrustRoot = userTrustRootCertificate || machineTrustRootCertificate;

        MachineTrustRoot = machineTrustRootCertificate;
        TrustRootAsAdministrator = trustRootCertificateAsAdmin;

        if (rootCertificateName != null) RootCertificateName = rootCertificateName;

        if (rootCertificateIssuerName != null) RootCertificateIssuerName = rootCertificateIssuerName;

        CertificateEngine = CertificateEngine.Pure;

        this.loggerFactory = loggerFactory ?? new NullLoggerFactory();

        this.logger = this.loggerFactory.CreateLogger<CertificateManager>();
        this.logger.LogTrace("Constructor called ({RootCertificateName}, {RootCertificateIssuerName}, {UserTrustRootCertificate}, {MachineTrustRootCertificate}, {TrustRootCertificateAsAdmin})", rootCertificateName, rootCertificateIssuerName, userTrustRootCertificate, machineTrustRootCertificate, trustRootCertificateAsAdmin);
        this.certificateCache = new DefaultCertificateDiskCache(this.loggerFactory.CreateLogger<DefaultCertificateDiskCache>());
    }

    private ICertificateGenerator CertEngine
    {
        get
        {
            certEngineValue ??= engine switch
            {
                CertificateEngine.BouncyCastle => new BcCertificateMaker(CertificateValidDays),
                CertificateEngine.BouncyCastleFast => new BcCertificateMakerFast(CertificateValidDays),
                CertificateEngine.Pure => new DotnetCertificateMaker(CertificateValidDays),
                _ => new WinCertificateMaker(CertificateValidDays),
            };
            return certEngineValue;
        }
    }

    /// <summary>
    /// Is the root certificate used by this proxy is valid?
    /// </summary>
    internal bool CertValidated => RootCertificate != null;

    /// <summary>
    /// Trust the RootCertificate used by this proxy server for current user
    /// </summary>
    internal bool UserTrustRoot { get; set; }

    /// <summary>
    /// Trust the RootCertificate used by this proxy server for current machine
    /// Needs elevated permission, otherwise will fail silently.
    /// </summary>
    internal bool MachineTrustRoot { get; set; }

    /// <summary>
    /// Whether trust operations should be done with elevated privileges
    /// Will prompt with UAC if required. Works only on Windows.
    /// </summary>
    internal bool TrustRootAsAdministrator { get; set; }

    /// <summary>
    /// Select Certificate Engine.
    /// Optionally set to BouncyCastle.
    /// Mono only support BouncyCastle and it is the default.
    /// </summary>
    [Obsolete("This property will be removed in future versions, just a pure C# version available")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CertificateEngine CertificateEngine
    {
        get => engine;
        set
        {
            // For Mono (or Non-Windows) only Bouncy Castle is supported
            if (value == CertificateEngine.DefaultWindows && !RunTime.IsWindows)
            {
                var ex = new PlatformNotSupportedException("Windows Certificate Engine is only supported on Windows OS.");
                logger.LogError(ex, "Windows Certificate Engine is only supported on Windows OS.");
                throw ex;
            }

            if (value != engine)
            {
                certEngineValue = null!;
                engine = value;
            }
        }
    }

    /// <summary>
    /// Password of the Root certificate file.
    /// <para>Set a password for the .pfx file</para>
    /// </summary>
    public string PfxPassword { get; set; } = string.Empty;

    /// <summary>
    /// Name(path) of the Root certificate file.
    /// <para>
    /// Set the name(path) of the .pfx file. If it is string.Empty Root certificate file will be named as
    /// "rootCert.pfx" (and will be saved in proxy dll directory)
    /// </para>
    /// </summary>
    public string PfxFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Number of Days generated HTTPS certificates are valid for.
    /// Maximum allowed on iOS 13 is 825 days and it is the default.
    /// </summary>
    public int CertificateValidDays { get; set; } = 825;

    /// <summary>
    /// Name of the root certificate issuer.
    /// (This is valid only when RootCertificate property is not set.)
    /// </summary>
    public string RootCertificateIssuerName
    {
        get => issuer ?? DefaultRootCertificateIssuer;
        set => issuer = value;
    }

    /// <summary>
    /// Name of the root certificate.
    /// (This is valid only when RootCertificate property is not set.)
    /// If no certificate is provided then a default Root Certificate will be created and used.
    /// The provided root certificate will be stored in proxy exe directory with the private key.
    /// Root certificate file will be named as "rootCert.pfx".
    /// </summary>
    public string RootCertificateName
    {
        get => rootCertificateName ?? DefaultRootRootCertificateName;
        set => rootCertificateName = value;
    }

    /// <summary>
    /// The root certificate.
    /// </summary>
    public X509Certificate2? RootCertificate
    {
        get => rootCertificate;
        set
        {
            ClearRootCertificate();
            rootCertificate = value;
        }
    }

    /// <summary>
    /// Save all fake certificates using <seealso cref="CertificateStorage" />.
    /// <para>for can load the certificate and not make new certificate every time. </para>
    /// </summary>
    public bool SaveFakeCertificates { get; set; } = false;

    /// <summary>
    /// The fake certificate cache storage.
    /// The default cache storage implementation saves certificates in folder "crts" (will be created in proxy dll
    /// directory).
    /// Implement ICertificateCache interface and assign concrete class here to customize.
    /// </summary>
    public ICertificateCache CertificateStorage
    {
        get => certificateCache;
        set => certificateCache = value ?? new DefaultCertificateDiskCache(loggerFactory.CreateLogger<DefaultCertificateDiskCache>());
    }

    /// <summary>
    /// Overwrite Root certificate file.
    /// <para>true : replace an existing .pfx file if password is incorrect or if RootCertificate = null.</para>
    /// </summary>
    public bool OverwritePfxFile { get; set; } = true;

    /// <summary>
    /// Minutes certificates should be kept in cache when not used.
    /// </summary>
    public int CertificateCacheTimeOutMinutes { get; set; } = 60;

    /// <summary>
    /// Adjust behaviour when certificates are saved to filesystem.
    /// </summary>
    public X509KeyStorageFlags StorageFlag { get; set; } = X509KeyStorageFlags.Exportable;

    /// <summary>
    /// Disable wild card certificates. Disabled by default.
    /// </summary>
    public bool DisableWildCardCertificates { get; set; } = false;

    /// <inheritdoc />
    public void Dispose ()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// For CertificateEngine.DefaultWindows to work we need to also check in personal store
    /// </summary>
    /// <param name="storeLocation"></param>
    /// <returns></returns>
    private bool RootCertificateInstalled ( StoreLocation storeLocation )
    {
        if (RootCertificate == null) return false;

        var value = $"{RootCertificate.Issuer}";
        return HasCertificateInStore(StoreName.Root, storeLocation, value)
               && (CertificateEngine != CertificateEngine.DefaultWindows
                   || HasCertificateInStore(StoreName.My, storeLocation, value));
    }

    /// <summary>
    /// Check if certificate exists in store
    /// </summary>
    /// <param name="storeName">Which store to check</param>
    /// <param name="storeLocation">Which store location to check</param>
    /// <param name="findValue">Value for <see cref="X509FindType.FindBySubjectDistinguishedName"/></param>
    /// <returns></returns>
    private static bool HasCertificateInStore ( StoreName storeName, StoreLocation storeLocation,
        string findValue )
    {
        using var x509Store = new X509Store(storeName, storeLocation);
        try
        {
            x509Store.Open(OpenFlags.OpenExistingOnly);
            var certs = x509Store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, findValue, false);
            return certs.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Make current machine trust the Root Certificate used by this proxy
    /// </summary>
    /// <param name="storeName"></param>
    /// <param name="storeLocation"></param>
    private void InstallCertificate ( StoreName storeName, StoreLocation storeLocation )
    {
        if (RootCertificate == null)
        {
            var ex = new InvalidOperationException("Could not install certificate as it is null or empty.");
            logger.LogError(ex, "Could not install certificate as it is null or empty.");
            throw ex;
        }

        using var x509Store = new X509Store(storeName, storeLocation);

        // todo
        // also it should do not duplicate if certificate already exists
        try
        {
            x509Store.Open(OpenFlags.ReadWrite);
            x509Store.Add(RootCertificate);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to make system trust root certificate for {storeName}\\{storeLocation} store location. You may need admin rights.", storeName, storeLocation);
        }
    }

    /// <summary>
    /// Remove the Root Certificate trust
    /// </summary>
    /// <param name="storeName"></param>
    /// <param name="storeLocation"></param>
    /// <param name="certificate"></param>
    private void UninstallCertificate ( StoreName storeName, StoreLocation storeLocation, X509Certificate2? certificate )
    {
        if (certificate == null)
        {
            logger.LogWarning("Could not remove certificate as it is null or empty.");
            return;
        }

        using var x509Store = new X509Store(storeName, storeLocation);

        try
        {
            x509Store.Open(OpenFlags.ReadWrite);

            x509Store.Remove(certificate);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove root certificate trust for {storeName}\\{storeLocation} store location. You may need admin rights.", storeName, storeLocation);
        }
    }

    /// <summary>
    /// Generate a certificate using the Certificate Engine specified in <see cref="CertificateEngine"/> and makes sure the root cert exists
    /// </summary>
    private async Task<X509Certificate2> GenerateCertificateAsync ( string certificateName, bool isRootCertificate, CancellationToken cancellationToken = default )
    {
        logger.LogTrace("MakeCertificate(certificateName: {CertificateName}, isRootCertificate: {IsRootCertificate}) called", certificateName, isRootCertificate);
        if (!isRootCertificate && RootCertificate == null)
        {
            await LoadOrCreateRootCertificateAsync(cancellationToken: cancellationToken);
        }

        var certificate = CertEngine.GenerateCertificate(certificateName, isRootCertificate ? null : RootCertificate);

        if (CertificateEngine == CertificateEngine.DefaultWindows)
        {
            await Task.Run(() => UninstallCertificate(StoreName.My, StoreLocation.CurrentUser, certificate), cancellationToken);
        }


        return certificate;
    }

    internal async Task<X509Certificate2?> GetCertificateFromDiskOrGenerateAsync ( string certificateName, bool isRootCertificate, CancellationToken cancellationToken = default )
    {
        logger.LogTrace("GetX509Certificate2Async({certificateName}, {isRootCertificate}) called", certificateName, isRootCertificate);
        X509Certificate2? certificate;

        try
        {
            if (!isRootCertificate && SaveFakeCertificates)
            {
                var subjectName = ProxyConstants.CnRemoverRegex
                    .Replace(certificateName, string.Empty)
                    .Replace("*", "$x$");

                try
                {
                    certificate = await certificateCache.LoadCertificateAsync(subjectName, StorageFlag, cancellationToken);

                    if (certificate != null && certificate.NotAfter <= DateTime.Now)
                    {
                        logger.LogWarning("Cached certificate for {subjectName} has expired.", subjectName);
                        certificate = null;
                    }
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Failed to load fake certificate for {subjectName}.", subjectName);
                    certificate = null;
                }

                if (certificate == null)
                {
                    certificate = await GenerateCertificateAsync(certificateName, false, cancellationToken);
                    try
                    {
                        await certificateCache.SaveCertificateAsync(subjectName, certificate, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Failed to save fake certificate for {subjectName}.", subjectName);
                    }
                }
            }
            else
            {
                certificate = await GenerateCertificateAsync(certificateName, isRootCertificate, cancellationToken);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create certificate for {certificateName}.", certificateName);
            certificate = null;
        }

        return certificate;
    }

    /// <summary>
    /// Get a certificate from cache, disk or generate a new one using the configured <see cref="RootCertificate"/>
    /// </summary>
    /// <param name="certificateName">Name of the certificate, which will be both the subject and the altervative name</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <remarks>Tries memory cache, disk cache and lastly generates new certificate</remarks>
    public async Task<X509Certificate2?> GetOrGenerateCertificateAsync ( string certificateName, CancellationToken cancellationToken = default )
    {
        var cachedCert = await cachedCertificates.GetOrAddAsync(certificateName, async ( facCancellation ) =>
        {
            return new CachedCertificate((await GetCertificateFromDiskOrGenerateAsync(certificateName, false, facCancellation))!);
        }, cancellationToken);

        return cachedCert?.Certificate;
    }

    /// <summary>
    /// Starts a never ending task, that is cancellable by calling <see cref="StopClearingCertificates"/>
    /// </summary>
    internal async void StartClearingCertificates ()
    {
        var cancellationToken = clearCertificatesTokenSource.Token;
        while (!cancellationToken.IsCancellationRequested)
        {
            var cutOff = DateTime.UtcNow.AddMinutes(-CertificateCacheTimeOutMinutes);

            var outdated = cachedCertificates.Where(x => x.Value.LastAccess < cutOff).ToList();

            foreach (var cache in outdated)
            {
                cachedCertificates.TryRemove(cache.Key, out _);
            }

            // after a minute come back to check for outdated certificates in cache
            try
            {
                await Task.Delay(1000 * 60, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Stops the certificate cache clear process
    /// </summary>
    internal void StopClearingCertificates ()
    {
        clearCertificatesTokenSource.Cancel();
    }

    /// <summary>
    /// Attempts to load or create the <see cref="RootCertificate"/>.
    /// </summary>
    /// <param name="persistToFile">if set to <c>true</c> try to load/save the certificate from rootCert.pfx.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// true if succeeded, else false.
    /// </returns>
    /// <remarks>Will use the data in <see cref="RootCertificateName"/> and <see cref="RootCertificateIssuerName"/> to generate a root cert if not found</remarks>
    public async Task<bool> LoadOrCreateRootCertificateAsync ( bool persistToFile = true, CancellationToken cancellationToken = default )
    {
        logger.LogTrace("LoadOrCreateRootCertificateAsync({PersistToFile}) called", persistToFile);

        await rootCertCreationLock.WaitAsync(cancellationToken);
        try
        {
            if (persistToFile && RootCertificate == null)
            {
                RootCertificate = await LoadRootCertificateAsync(cancellationToken);
            }

            if (RootCertificate != null)
            {
                logger.LogTrace("Root certificate loaded from file");
                return true;
            }

            if (!OverwritePfxFile)
            {
                try
                {
                    var rootCert = await certificateCache.LoadRootCertificateAsync(PfxFilePath, PfxPassword,
                        X509KeyStorageFlags.Exportable, cancellationToken);

                    if (rootCert != null && rootCert.NotAfter <= DateTime.Now)
                    {
                        logger.LogWarning("Loaded root certificate has expired.");
                        return false;
                    }

                    if (rootCert != null)
                    {
                        RootCertificate = rootCert;
                        return true;
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Root cert cannot be loaded.");
                }
            }

            try
            {
                logger.LogTrace("Generating new root certificate");
                RootCertificate = await GetCertificateFromDiskOrGenerateAsync(RootCertificateName, true, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Root cert cannot be created.");
            }

            if (persistToFile && RootCertificate != null)
                try
                {
                    await certificateCache.SaveRootCertificateAsync(PfxFilePath, PfxPassword, RootCertificate, cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Root cert cannot be saved.");
                }
            logger.LogTrace("CreateRootCertificate() finished");

            return RootCertificate != null;
        }
        finally
        {
            rootCertCreationLock.Release();
        }
    }

    /// <summary>
    /// Loads root certificate from current executing assembly location with expected name rootCert.pfx.
    /// </summary>
    /// <returns></returns>
    public async Task<X509Certificate2?> LoadRootCertificateAsync ( CancellationToken cancellationToken )
    {
        logger.LogTrace("LoadRootCertificate() called");

        try
        {
            var rootCert =
                await certificateCache.LoadRootCertificateAsync(PfxFilePath, PfxPassword, X509KeyStorageFlags.Exportable, cancellationToken);

            if (rootCert != null && rootCert.NotAfter <= DateTime.Now)
            {
                logger.LogError("Loaded root certificate has expired.");
                return null;
            }
            logger.LogTrace("LoadRootCertificate() finished");

            return rootCert;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Root cert cannot be loaded.");
            return null;
        }
    }

    /// <summary>
    /// Trusts the root certificate in user store, optionally also in machine store.
    /// Machine trust would require elevated permissions (will silently fail otherwise).
    /// </summary>
    public void TrustRootCertificate ( bool machineTrusted = false )
    {
        logger.LogTrace("TrustRootCertificate({MachineTrusted}) called", machineTrusted);

        // currentUser\personal
        InstallCertificate(StoreName.My, StoreLocation.CurrentUser);

        if (!machineTrusted)
        {
            // currentUser\Root
            InstallCertificate(StoreName.Root, StoreLocation.CurrentUser);
        }
        else
        {
            // current system
            InstallCertificate(StoreName.My, StoreLocation.LocalMachine);

            // this adds to both currentUser\Root & currentMachine\Root
            InstallCertificate(StoreName.Root, StoreLocation.LocalMachine);
        }
    }

    /// <summary>
    /// Puts the certificate to the user store, optionally also to machine store.
    /// Prompts with UAC if elevated permissions are required. Works only on Windows.
    /// </summary>
    /// <returns>True if success.</returns>
    public bool TrustRootCertificateAsAdmin ( bool machineTrusted = false )
    {
        logger.LogTrace("TrustRootCertificateAsAdmin({MachineTrusted}) called", machineTrusted);

        if (!RunTime.IsWindows) return false;

        // currentUser\Personal
        InstallCertificate(StoreName.My, StoreLocation.CurrentUser);

        var pfxFileName = Path.GetTempFileName();
        File.WriteAllBytes(pfxFileName, RootCertificate!.Export(X509ContentType.Pkcs12, PfxPassword));

        // currentUser\Root, currentMachine\Personal &  currentMachine\Root
        var info = new ProcessStartInfo
        {
            FileName = "certutil.exe",
            CreateNoWindow = true,
            UseShellExecute = true,
            Verb = "runas",
            ErrorDialog = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (!machineTrusted)
            info.Arguments = "-f -user -p \"" + PfxPassword + "\" -importpfx root \"" + pfxFileName + "\"";
        else
            info.Arguments = "-importPFX -p \"" + PfxPassword + "\" -f \"" + pfxFileName + "\"";

        try
        {
            var process = Process.Start(info);
            if (process == null) return false;

            process.WaitForExit();
            File.Delete(pfxFileName);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to trust root certificate machine-trusted = {machineTrusted}.", machineTrusted);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensure certificates are setup (creates root if required).
    /// Also makes root certificate trusted based on initial setup from proxy constructor for user/machine trust.
    /// </summary>
    public async Task EnsureRootCertificateAsync ( CancellationToken cancellationToken = default )
    {
        logger.LogTrace("EnsureRootCertificate() called");
        if (RootCertificate is null)
        {
            await LoadOrCreateRootCertificateAsync(cancellationToken: cancellationToken);
        }

        if (TrustRootAsAdministrator)
        {
            TrustRootCertificateAsAdmin(MachineTrustRoot);
        }

        else if (UserTrustRoot)
        {
            TrustRootCertificate(MachineTrustRoot);
        }
        logger.LogTrace("EnsureRootCertificate() finished");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userTrustRootCertificate"></param>
    /// <param name="machineTrustRootCertificate"></param>
    /// <param name="trustRootCertificateAsAdmin"></param>
    [Obsolete("This method will be removed in future versions. Use EnsureRootCertificateAsync instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void EnsureRootCertificate ( bool userTrustRootCertificate,
        bool machineTrustRootCertificate, bool trustRootCertificateAsAdmin = false )
    {
        UserTrustRoot = userTrustRootCertificate || machineTrustRootCertificate;
        MachineTrustRoot = machineTrustRootCertificate;
        TrustRootAsAdministrator = trustRootCertificateAsAdmin;

        EnsureRootCertificateAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Determines whether the root certificate is trusted.
    /// </summary>
    public bool IsRootCertificateUserTrusted ()
    {
        return RootCertificateInstalled(StoreLocation.CurrentUser) || IsRootCertificateMachineTrusted();
    }

    /// <summary>
    /// Determines whether the root certificate is machine trusted.
    /// </summary>
    public bool IsRootCertificateMachineTrusted ()
    {
        return RootCertificateInstalled(StoreLocation.LocalMachine);
    }

    /// <summary>
    /// Removes the trusted certificates from user store, optionally also from machine store.
    /// To remove from machine store elevated permissions are required (will fail silently otherwise).
    /// </summary>
    /// <param name="machineTrusted">Should also remove from machine store?</param>
    public void RemoveTrustedRootCertificate ( bool machineTrusted = false )
    {
        // currentUser\personal
        UninstallCertificate(StoreName.My, StoreLocation.CurrentUser, RootCertificate);

        if (!machineTrusted)
        {
            // currentUser\Root
            UninstallCertificate(StoreName.Root, StoreLocation.CurrentUser, RootCertificate);
        }
        else
        {
            // current system
            UninstallCertificate(StoreName.My, StoreLocation.LocalMachine, RootCertificate);

            // this adds to both currentUser\Root & currentMachine\Root
            UninstallCertificate(StoreName.Root, StoreLocation.LocalMachine, RootCertificate);
        }
    }

    /// <summary>
    /// Removes the trusted certificates from user store, optionally also from machine store
    /// </summary>
    /// <returns>Should also remove from machine store?</returns>
    public bool RemoveTrustedRootCertificateAsAdmin ( bool machineTrusted = false )
    {
        if (!RunTime.IsWindows) return false;

        // currentUser\Personal
        UninstallCertificate(StoreName.My, StoreLocation.CurrentUser, RootCertificate);

        var infos = new List<ProcessStartInfo>();
        if (!machineTrusted)
            infos.Add(new ProcessStartInfo
            {
                FileName = "certutil.exe",
                Arguments = "-delstore -user Root \"" + RootCertificateName + "\"",
                CreateNoWindow = true,
                UseShellExecute = true,
                Verb = "runas",
                ErrorDialog = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        else
            infos.AddRange(
                [
                    // currentMachine\Personal
                    new()
                    {
                        FileName = "certutil.exe",
                        Arguments = "-delstore My \"" + RootCertificateName + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = true,
                        Verb = "runas",
                        ErrorDialog = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    },

                    // currentUser\Personal & currentMachine\Personal
                    new()
                    {
                        FileName = "certutil.exe",
                        Arguments = "-delstore Root \"" + RootCertificateName + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = true,
                        Verb = "runas",
                        ErrorDialog = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                ]);

        var success = true;
        try
        {
            foreach (var info in infos)
            {
                var process = Process.Start(info);

                if (process is null)
                {
                    success = false;
                    continue;
                }

                process.WaitForExit();
            }
        }
        catch
        {
            success = false;
        }

        return success;
    }

    /// <summary>
    /// Clear the root certificate and cache.
    /// </summary>
    public void ClearRootCertificate ()
    {
        certificateCache.Clear();
        cachedCertificates.Clear();
        rootCertificate = null;
    }

    private void Dispose ( bool disposing )
    {
        if (disposed) return;

        if (disposing)
        {
            clearCertificatesTokenSource.Dispose();
        }

        disposed = true;
    }

    /// <inheritdoc />
    ~CertificateManager ()
    {
        Dispose(false);
    }
}
