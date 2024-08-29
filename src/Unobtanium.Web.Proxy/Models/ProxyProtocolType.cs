using System;

namespace Titanium.Web.Proxy.Models;
/// <summary>
/// The ProxyProtocolType enumeration represents the types of protocols that a proxy can support.
/// It is a flags enumeration, which means multiple values can be combined using bitwise operations.
/// The values include None (indicating no protocol), Http (for HTTP protocol), Https (for HTTPS protocol),
/// and AllHttp (for both HTTP and HTTPS protocols).
/// </summary>
[Flags]
public enum ProxyProtocolType
{
    /// <summary>
    ///     The none
    /// </summary>
    None = 0,

    /// <summary>
    ///     HTTP
    /// </summary>
    Http = 1,

    /// <summary>
    ///     HTTPS
    /// </summary>
    Https = 2,

    /// <summary>
    ///     Both HTTP and HTTPS
    /// </summary>
    AllHttp = Http | Https
}
