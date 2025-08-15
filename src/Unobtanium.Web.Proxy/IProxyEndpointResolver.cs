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
    public int? Port { get; }
    public int? HttpsPort { get; }

    internal void SetPorts ( int? port, int? httpsPort );
}
