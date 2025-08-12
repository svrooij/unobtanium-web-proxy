using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Proxy;
public class ProxyEndpoint
{
    public required IPAddress Address { get; set; }
    internal int _port = 0;
    public int Port { get => _port; init { _port = value; } }
    internal TcpListener? Listener { get; set; } = null;
}
