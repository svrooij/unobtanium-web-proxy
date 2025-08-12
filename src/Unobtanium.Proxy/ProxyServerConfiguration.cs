using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;

namespace Unobtanium.Proxy;
public class ProxyServerConfiguration
{
    public int ConnectionTimeout { get; init; } = 30; // seconds
    public int DefaultPort { get; init; } = 8000;
    public List<ProxyEndpoint> Endpoints { get; init; } = new List<ProxyEndpoint>();
    public ProxyServerEvents Events { get; init; } = new ProxyServerEvents();
    public Func<string, CancellationToken, Task<bool>> TryDecryptHttps { get; set; } = (_, _) => Task.FromResult(true);

    /// <summary>
    ///     Customize the minimum ThreadPool size (increase it on a server)
    /// </summary>
    public int ThreadPoolWorkerThread { get; set; } = Environment.ProcessorCount;
}
