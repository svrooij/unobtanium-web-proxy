﻿/*
    Copyright © 2002, The KPD-Team
    All rights reserved.
    http://www.mentalis.org/

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    - Redistributions of source code must retain the above copyright
       notice, this list of conditions and the following disclaimer. 

    - Neither the name of the KPD-Team, nor the names of its contributors
       may be used to endorse or promote products derived from this
       software without specific prior written permission. 

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL
  THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
  SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
  HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
  STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
  OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unobtanium.Web.Proxy.ProxySocket.Authentication;

namespace Unobtanium.Web.Proxy.ProxySocket;

/// <summary>
///     Implements the SOCKS5 protocol.
/// </summary>
internal sealed class Socks5Handler : SocksHandler
{
    private const int ConnectOffset = 4;

    /// <summary>
    ///     The length of the connect request.
    /// </summary>
    private int handShakeLength;

    // private variables
    /// <summary>Holds the value of the Password property.</summary>
    private string password = string.Empty;

    /// <summary>
    ///     Initializes a new Socks5Handler instance.
    /// </summary>
    /// <param name="server">The socket connection with the proxy server.</param>
    /// <exception cref="ArgumentNullException"><c>server</c>  is null.</exception>
    public Socks5Handler ( Socket server ) : this(server, "")
    {
    }

    /// <summary>
    ///     Initializes a new Socks5Handler instance.
    /// </summary>
    /// <param name="server">The socket connection with the proxy server.</param>
    /// <param name="user">The username to use.</param>
    /// <exception cref="ArgumentNullException"><c>server</c> -or- <c>user</c> is null.</exception>
    public Socks5Handler ( Socket server, string user ) : this(server, user, "")
    {
    }

    /// <summary>
    ///     Initializes a new Socks5Handler instance.
    /// </summary>
    /// <param name="server">The socket connection with the proxy server.</param>
    /// <param name="user">The username to use.</param>
    /// <param name="pass">The password to use.</param>
    /// <exception cref="ArgumentNullException"><c>server</c> -or- <c>user</c> -or- <c>pass</c> is null.</exception>
    public Socks5Handler ( Socket server, string user, string pass ) : base(server, user)
    {
        Password = pass;
    }

    /// <summary>
    ///     Gets or sets the password to use when authenticating with the SOCKS5 server.
    /// </summary>
    /// <value>The password to use when authenticating with the SOCKS5 server.</value>
    private string Password
    {
        get => password;
        set => password = value ?? throw new ArgumentNullException(nameof(Password));
    }

    /// <summary>
    ///     Starts the synchronous authentication process.
    /// </summary>
    /// <exception cref="ProxyException">Authentication with the proxy server failed.</exception>
    /// <exception cref="ProtocolViolationException">The proxy server uses an invalid protocol.</exception>
    /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
    /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
    private void Authenticate ( byte[] buffer )
    {
        buffer[0] = 5;
        buffer[1] = 2;
        buffer[2] = 0;
        buffer[3] = 2;
        if (Server.Send(buffer, 0, 4, SocketFlags.None) < 4)
            throw new SocketException(10054);

        ReadBytes(buffer, 2);
        if (buffer[1] == 255)
            throw new ProxyException("No authentication method accepted.");
        AuthMethod authenticate = buffer[1] switch
        {
            0 => new AuthNone(Server),
            2 => new AuthUserPass(Server, Username, Password),
            _ => throw new ProtocolViolationException(),
        };
        authenticate.Authenticate();
    }

    /// <summary>
    ///     Creates an array of bytes that has to be sent when the user wants to connect to a specific host/port combination.
    /// </summary>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="buffer">The buffer which contains the result data.</param>
    /// <returns>An array of bytes that has to be sent when the user wants to connect to a specific host/port combination.</returns>
    /// <exception cref="ArgumentNullException"><c>host</c> is null.</exception>
    /// <exception cref="ArgumentException"><c>port</c> or <c>host</c> is invalid.</exception>
    private int GetHostPortBytes ( string host, int port, Memory<byte> buffer )
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.Length == 0 || host.Length > 255)
            throw new ArgumentException("Hostname invalid", nameof(host));

        if (port <= 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 0 and 65535");

        var length = 7 + host.Length;
        if (buffer.Length < length)
            throw new ArgumentException("Buffer is to small", nameof(buffer));

        var connect = buffer.Span;
        connect[0] = 5;
        connect[1] = 1;
        connect[2] = 0; // reserved
        connect[3] = 3;
        connect[4] = (byte)host.Length;
        Encoding.ASCII.GetBytes(host).CopyTo(connect[5..]);
        PortToBytes(port, connect[(host.Length + 5)..]);
        return length;
    }

    /// <summary>
    ///     Creates an array of bytes that has to be sent when the user wants to connect to a specific IPEndPoint.
    /// </summary>
    /// <param name="remoteEp">The IPEndPoint to connect to.</param>
    /// <param name="buffer">The buffer which contains the result data.</param>
    /// <returns>An array of bytes that has to be sent when the user wants to connect to a specific IPEndPoint.</returns>
    /// <exception cref="ArgumentNullException"><c>remoteEP</c> is null.</exception>
    private int GetEndPointBytes ( IPEndPoint remoteEp, Memory<byte> buffer )
    {
        ArgumentNullException.ThrowIfNull(remoteEp);

        if (buffer.Length < 10)
            throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is smaller then 10");

        var connect = buffer.Span;
        connect[0] = 5;
        connect[1] = 1;
        connect[2] = 0; // reserved
        connect[3] = 1;
        remoteEp.Address.GetAddressBytes().CopyTo(connect[4..]);
        PortToBytes(remoteEp.Port, connect[8..]);
        return 10;
    }

    /// <summary>
    ///     Starts negotiating with the SOCKS server.
    /// </summary>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <exception cref="ArgumentNullException"><c>host</c> is null.</exception>
    /// <exception cref="ArgumentException"><c>port</c> is invalid.</exception>
    /// <exception cref="ProxyException">The proxy rejected the request.</exception>
    /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
    /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
    /// <exception cref="ProtocolViolationException">The proxy server uses an invalid protocol.</exception>
    public override void Negotiate ( string host, int port )
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(258, 10 + host.Length + Username.Length + Password.Length));
        try
        {
            Authenticate(buffer);

            var length = GetHostPortBytes(host, port, buffer);
            Negotiate(buffer, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///     Starts negotiating with the SOCKS server.
    /// </summary>
    /// <param name="remoteEp">The IPEndPoint to connect to.</param>
    /// <exception cref="ArgumentNullException"><c>remoteEP</c> is null.</exception>
    /// <exception cref="ProxyException">The proxy rejected the request.</exception>
    /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
    /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
    /// <exception cref="ProtocolViolationException">The proxy server uses an invalid protocol.</exception>
    public override void Negotiate ( IPEndPoint remoteEp )
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(258, 13 + Username.Length + Password.Length));
        try
        {
            Authenticate(buffer);

            var length = GetEndPointBytes(remoteEp, buffer);
            Negotiate(buffer, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///     Starts negotiating with the SOCKS server.
    /// </summary>
    /// <param name="buffer">The bytes to send when trying to authenticate.</param>
    /// <param name="length">The byte count to send when trying to authenticate.</param>
    /// <exception cref="ArgumentNullException"><c>connect</c> is null.</exception>
    /// <exception cref="ArgumentException"><c>connect</c> is too small.</exception>
    /// <exception cref="ProxyException">The proxy rejected the request.</exception>
    /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
    /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
    /// <exception cref="ProtocolViolationException">The proxy server uses an invalid protocol.</exception>
    private void Negotiate ( byte[] buffer, int length )
    {
        if (Server.Send(buffer, 0, length, SocketFlags.None) < length)
            throw new SocketException(10054);

        ReadBytes(buffer, 4);
        if (buffer[1] != 0)
        {
            Server.Close();
            throw new ProxyException(buffer[1]);
        }

        switch (buffer[3])
        {
            case 1:
                ReadBytes(buffer, 6); // IPv4 address with port
                break;
            case 3:
                ReadBytes(buffer, 1); // domain name length
                ReadBytes(buffer, buffer[0] + 2); // domain name with port
                break;
            case 4:
                ReadBytes(buffer, 18); //IPv6 address with port
                break;
            default:
                Server.Close();
                throw new ProtocolViolationException();
        }
    }

    /// <summary>
    ///     Starts negotiating asynchronously with the SOCKS server.
    /// </summary>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="callback">The method to call when the negotiation is complete.</param>
    /// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
    /// <param name="state">The state.</param>
    /// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
    public override AsyncProxyResult BeginNegotiate ( string host, int port, HandShakeComplete callback,
        IPEndPoint proxyEndPoint, object state )
    {
        ProtocolComplete = callback;
        Buffer = ArrayPool<byte>.Shared.Rent(Math.Max(258, 10 + host.Length + Username.Length + Password.Length));

        // first {ConnectOffset} bytes are reserved for authentication 
        handShakeLength = GetHostPortBytes(host, port, Buffer.AsMemory(ConnectOffset));
        Server.BeginConnect(proxyEndPoint, OnConnect, Server);
        AsyncResult = new AsyncProxyResult(state);
        return AsyncResult;
    }

    /// <summary>
    ///     Starts negotiating asynchronously with the SOCKS server.
    /// </summary>
    /// <param name="remoteEp">An IPEndPoint that represents the remote device.</param>
    /// <param name="callback">The method to call when the negotiation is complete.</param>
    /// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
    /// <param name="state">The state.</param>
    /// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
    public override AsyncProxyResult BeginNegotiate ( IPEndPoint remoteEp, HandShakeComplete callback,
        IPEndPoint proxyEndPoint, object state )
    {
        ProtocolComplete = callback;
        Buffer = ArrayPool<byte>.Shared.Rent(Math.Max(258, 13 + Username.Length + Password.Length));

        // first {ConnectOffset} bytes are reserved for authentication 
        handShakeLength = GetEndPointBytes(remoteEp, Buffer.AsMemory(ConnectOffset));
        Server.BeginConnect(proxyEndPoint, OnConnect, Server);
        AsyncResult = new AsyncProxyResult(state);
        return AsyncResult;
    }

    /// <summary>
    ///     Called when the socket is connected to the remote server.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnConnect ( IAsyncResult ar )
    {
        try
        {
            Server.EndConnect(ar);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
            return;
        }

        try
        {
            Buffer![0] = 5;
            Buffer[1] = 2;
            Buffer[2] = 0;
            Buffer[3] = 2;
            Server.BeginSend(Buffer, 0, 4, SocketFlags.None, OnAuthSent,
                Server);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
        }
    }

    /// <summary>
    ///     Called when the authentication bytes have been sent.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnAuthSent ( IAsyncResult ar )
    {
        try
        {
            HandleEndSend(ar, 4);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
            return;
        }

        try
        {
            BufferCount = 2;
            Received = 0;
            Server.BeginReceive(Buffer!, 0, BufferCount, SocketFlags.None, OnAuthReceive,
                Server);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
        }
    }

    /// <summary>
    ///     Called when an authentication reply has been received.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnAuthReceive ( IAsyncResult ar )
    {
        try
        {
            HandleEndReceive(ar);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
            return;
        }

        try
        {
            if (Received < BufferCount)
            {
                Server.BeginReceive(Buffer!, Received, BufferCount - Received, SocketFlags.None,
                    OnAuthReceive, Server);
            }
            else
            {
                AuthMethod authenticate;
                switch (Buffer![1])
                {
                    case 0:
                        authenticate = new AuthNone(Server);
                        break;
                    case 2:
                        authenticate = new AuthUserPass(Server, Username, Password);
                        break;
                    default:
                        OnProtocolComplete(new SocketException());
                        return;
                }

                authenticate.BeginAuthenticate(OnAuthenticated);
            }
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
        }
    }

    /// <summary>
    ///     Called when the socket has been successfully authenticated with the server.
    /// </summary>
    /// <param name="e">The exception that has occurred while authenticating, or <em>null</em> if no error occurred.</param>
    private void OnAuthenticated ( Exception? e )
    {
        if (e != null)
        {
            OnProtocolComplete(e);
            return;
        }

        try
        {
            Server.BeginSend(Buffer!, ConnectOffset, handShakeLength, SocketFlags.None, OnSent,
                Server);
        }
        catch (Exception ex)
        {
            OnProtocolComplete(ex);
        }
    }

    /// <summary>
    ///     Called when the connection request has been sent.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnSent ( IAsyncResult ar )
    {
        try
        {
            HandleEndSend(ar, BufferCount - ConnectOffset);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
            return;
        }

        try
        {
            BufferCount = 5;
            Received = 0;
            Server.BeginReceive(Buffer!, 0, BufferCount, SocketFlags.None, OnReceive,
                Server);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
        }
    }

    /// <summary>
    ///     Called when a connection reply has been received.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnReceive ( IAsyncResult ar )
    {
        try
        {
            HandleEndReceive(ar);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
            return;
        }

        try
        {
            if (Received == BufferCount)
                ProcessReply(Buffer!);
            else
                Server.BeginReceive(Buffer!, Received, BufferCount - Received, SocketFlags.None,
                    OnReceive, Server);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
        }
    }

    /// <summary>
    ///     Processes the received reply.
    /// </summary>
    /// <param name="buffer">The received reply</param>
    /// <exception cref="ProtocolViolationException">The received reply is invalid.</exception>
    private void ProcessReply ( byte[] buffer )
    {
        var lengthToRead = buffer[3] switch
        {
            1 => 5,//IPv4 address with port - 1 byte
            3 => buffer[4] + 2,//domain name with port
            4 => 17,//IPv6 address with port - 1 byte
            _ => throw new ProtocolViolationException(),
        };
        Received = 0;
        BufferCount = lengthToRead;
        Server.BeginReceive(Buffer!, 0, BufferCount, SocketFlags.None, OnReadLast, Server);
    }

    /// <summary>
    ///     Called when the last bytes are read from the socket.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnReadLast ( IAsyncResult ar )
    {
        try
        {
            HandleEndReceive(ar);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
            return;
        }

        try
        {
            if (Received == BufferCount)
                OnProtocolComplete(null);
            else
                Server.BeginReceive(Buffer!, Received, BufferCount - Received, SocketFlags.None,
                    OnReadLast, Server);
        }
        catch (Exception e)
        {
            OnProtocolComplete(e);
        }
    }
}
