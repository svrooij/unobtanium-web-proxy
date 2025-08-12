using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Proxy.Tcp;
public class TcpClientConnection : IDisposable
{
    private readonly Socket tcpClientSocket;
    
    private Stream? stream;

    public TcpClientConnection ( Socket tcpClientSocket, Activity? activity )
    {
        this.tcpClientSocket = tcpClientSocket;
        Activity = activity;
    }

    public readonly Activity? Activity;

    public Stream GetStream => stream ??= new NetworkStream(tcpClientSocket, true);

    public void Dispose ()
    {
        stream?.Dispose();
        if (tcpClientSocket.Connected)
        {
            try
            {
                tcpClientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException) { /* Ignore */ }
        }
        Activity?.Dispose();
    }
}
