using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;

namespace Unobtanium.Web.Proxy.Internal;
internal class ConnectionMapper : IDisposable
{
    public readonly ConcurrentDictionary<string, ClientDetails> Connections = new(StringComparer.OrdinalIgnoreCase);

    public void Dispose ()
    {
        Connections.Clear();
    }
}
