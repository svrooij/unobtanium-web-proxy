using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Unobtanium.Web.Proxy.Models;

/// <summary>
///     An abstract endpoint where the proxy listens
/// </summary>
public abstract class ProxyEndPoint
{
    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <param name="port"></param>
    /// <param name="decryptSsl"></param>
    protected ProxyEndPoint ( IPAddress ipAddress, int port, bool decryptSsl )
    {
        IpAddress = ipAddress;
        Port = port;
        DecryptSsl = decryptSsl;
    }

    /// <summary>
    ///     underlying TCP Listener object
    /// </summary>
    internal TcpListener? Listener { get; set; }

    /// <summary>
    ///     Ip Address we are listening.
    /// </summary>
    public IPAddress IpAddress { get; }

    /// <summary>
    ///     Port we are listening.
    /// </summary>
    public int Port { get; internal set; }

    /// <summary>
    ///     Enable SSL?
    /// </summary>
    public bool DecryptSsl { get; }

    /// <summary>
    ///     Generic certificate to use for SSL decryption.
    /// </summary>
    public X509Certificate2? GenericCertificate { get; set; }
}

internal class ProxyEndPointComparer : IEqualityComparer<ProxyEndPoint>
{
    public bool Equals ( ProxyEndPoint? x, ProxyEndPoint? y )
    {
        return x?.IpAddress.Equals(y?.IpAddress) == true && x.Port == y.Port;
    }

    public int GetHashCode ( ProxyEndPoint obj )
    {
        return HashCode.Combine(obj.IpAddress, obj.Port);
    }
}
