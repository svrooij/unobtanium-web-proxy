using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy;
/// <summary>
/// An interface that allows you to load the proxy ports at runtime.
/// </summary>
public interface IProxyEndpointResolver
{
    /// <summary>
    /// Port where the proxy is listening for HTTP requests.
    /// </summary>
    public int? Port { get; }

    /// <summary>
    /// Port that is used for HTTPS inspection, you should not use this port directly.
    /// </summary>
    public int? HttpsPort { get; }

    /// <summary>
    /// Internally we need a way to set the ports for the proxy server.
    /// </summary>
    /// <param name="port"></param>
    /// <param name="httpsPort"></param>
    internal void SetPorts ( int? port, int? httpsPort );
}
