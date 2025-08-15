using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Services;
internal class ProxyEndpointResolver : IProxyEndpointResolver
{
    public int? Port { get; set; }

    public int? HttpsPort { get; set; }

    void IProxyEndpointResolver.SetPorts ( int? port, int? httpsPort )
    {
        if (port > 0)
        {
            Port = port.Value;
        }

        if (httpsPort > 0)
        {
            HttpsPort = httpsPort.Value;
        }
    }
}
