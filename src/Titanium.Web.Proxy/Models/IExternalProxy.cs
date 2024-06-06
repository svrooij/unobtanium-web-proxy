namespace Titanium.Web.Proxy.Models;

/// <summary>
/// The IExternalProxy interface defines the contract for an external proxy.
/// It provides properties for configuring the proxy's credentials, host, port, and other settings.
/// </summary>
public interface IExternalProxy
{
    /// <summary>
    /// Gets or sets a value indicating whether to use default windows credentials.
    /// </summary>
    bool UseDefaultCredentials { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to bypass this proxy for connections to localhost.
    /// </summary>
    bool BypassLocalhost { get; set; }

    /// <summary>
    /// Gets or sets the type of the external proxy.
    /// </summary>
    ExternalProxyType ProxyType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to proxy DNS requests.
    /// </summary>
    bool ProxyDnsRequests { get; set; }

    /// <summary>
    /// Gets or sets the username for the proxy.
    /// </summary>
    string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the password for the proxy.
    /// </summary>
    string? Password { get; set; }

    /// <summary>
    /// Gets or sets the host name of the proxy.
    /// </summary>
    string HostName { get; set; }

    /// <summary>
    /// Gets or sets the port number of the proxy.
    /// </summary>
    int Port { get; set; }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    string ToString();
}