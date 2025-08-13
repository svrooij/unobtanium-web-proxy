namespace Unobtanium.Web.Proxy;

/// <summary>
/// Represents configuration options for a proxy server.
/// </summary>
/// <remarks>This class provides settings to configure the behavior of a proxy server, including ports,  system
/// proxy settings, and certificate handling. Use these options to customize the proxy  server's behavior before
/// starting it.</remarks>
public class ProxyServerOptions
{
    /// <summary>
    /// Gets or sets the port number used for the proxy server.
    /// </summary>
    /// <remarks>Use this port for both http and https proxy!</remarks>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the port number used for HTTPS connections.
    /// </summary>
    /// <remarks>You should not need this port, it is used internally to forward exncrypted traffic to</remarks>
    public int HttpsPort { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the application should configure itself as the system proxy.
    /// </summary>
    /// <remarks>Is not used yet</remarks>
    public bool SetAsSystemProxy { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the application should automatically trust certificates at startup.
    /// </summary>
    /// <remarks>Is not used yet.</remarks>
    public bool TrustCertificateOnStart { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the application should automatically trust certificates at startup (in user store).
    /// </summary>
    /// <remarks>Is not used yet.</remarks>
    public bool TrustCertificateOnStartAsUser { get; set; } = false;

    /// <summary>
    /// Specify a list of certificates that should be generated at startup and preloaded into the proxy server.
    /// </summary>
    /// <remarks>Use this for hosts you expect are going to be proxied</remarks>
    public string[]? PreloadCertificates { get; set; } = null;
}
