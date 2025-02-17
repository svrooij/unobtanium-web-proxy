﻿using System;
using System.Net;

namespace Unobtanium.Web.Proxy.Models;

/// <summary>
///     An upstream proxy this proxy uses if any.
/// </summary>
public class ExternalProxy : IExternalProxy
{
    private static readonly Lazy<NetworkCredential> defaultCredentials =
        new(() => CredentialCache.DefaultNetworkCredentials);

    private string? password;

    private string? userName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExternalProxy" /> class.
    /// </summary>
    public ExternalProxy ()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExternalProxy" /> class.
    /// </summary>
    /// <param name="hostName">Name of the host.</param>
    /// <param name="port">The port.</param>
    public ExternalProxy ( string hostName, int port )
    {
        HostName = hostName;
        Port = port;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExternalProxy" /> class.
    /// </summary>
    /// <param name="hostName">Name of the host.</param>
    /// <param name="port">The port.</param>
    /// <param name="userName">Name of the user.</param>
    /// <param name="password">The password.</param>
    public ExternalProxy ( string hostName, int port, string userName, string password )
    {
        HostName = hostName;
        Port = port;
        UserName = userName;
        Password = password;
    }

    /// <summary>
    ///     Use default windows credentials?
    /// </summary>
    public bool UseDefaultCredentials { get; set; }

    /// <summary>
    ///     Bypass this proxy for connections to localhost?
    /// </summary>
    public bool BypassLocalhost { get; set; }

    /// <summary>
    ///     Proxy type.
    /// </summary>
    public ExternalProxyType ProxyType { get; set; }

    /// <summary>
    ///     Should DNS requests be proxied?
    /// </summary>
    public bool ProxyDnsRequests { get; set; }

    /// <summary>
    ///     Username.
    /// </summary>
    public string? UserName
    {
        get => UseDefaultCredentials ? defaultCredentials.Value.UserName : userName;
        set
        {
            userName = value;

            if (defaultCredentials.Value.UserName != userName) UseDefaultCredentials = false;
        }
    }

    /// <summary>
    ///     Password.
    /// </summary>
    public string? Password
    {
        get => UseDefaultCredentials ? defaultCredentials.Value.Password : password;
        set
        {
            password = value;

            if (defaultCredentials.Value.Password != password) UseDefaultCredentials = false;
        }
    }

    /// <summary>
    ///     Host name.
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    ///     Port.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    ///     returns data in Hostname:port format.
    /// </summary>
    /// <returns></returns>
    public override string ToString ()
    {
        return $"{HostName}:{Port}";
    }
}

/// <summary>
/// Type of external proxy server.
/// </summary>
public enum ExternalProxyType
{
    /// <summary>A HTTP/HTTPS proxy server.</summary>
    Http,

    /// <summary>A SOCKS4[A] proxy server.</summary>
    Socks4,

    /// <summary>A SOCKS5 proxy server.</summary>
    Socks5
}
