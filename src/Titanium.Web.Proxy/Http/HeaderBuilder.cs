using System;
using System.Buffers;
using System.IO;
using System.Text;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Shared;

namespace Titanium.Web.Proxy.Http;

internal class HeaderBuilder
{
    private readonly MemoryStream stream = new();

    public void WriteRequestLine ( string httpMethod, string httpUrl, Version version )
    {
        // "{httpMethod} {httpUrl} HTTP/{version.Major}.{version.Minor}";
        Write($"{httpMethod} {httpUrl} HTTP/{version.Major}.{version.Minor}");
        //Write(httpMethod);
        //Write(" ");
        //Write(httpUrl);
        //Write(" HTTP/");
        //Write(version.Major.ToString());
        //Write(".");
        //Write(version.Minor.ToString());
        WriteLine();
    }

    public void WriteResponseLine ( Version version, int statusCode, string statusDescription )
    {
        // "HTTP/{version.Major}.{version.Minor} {statusCode} {statusDescription}";
        Write($"HTTP/{version.Major}.{version.Minor} {statusCode} {statusDescription}");
        //Write("HTTP/");
        //Write(version.Major.ToString());
        //Write(".");
        //Write(version.Minor.ToString());
        //Write(" ");
        //Write(statusCode.ToString());
        //Write(" ");
        //Write(statusDescription);
        WriteLine();
    }

    public void WriteHeaders ( HeaderCollection headers, bool sendProxyAuthorization = true,
        string? upstreamProxyUserName = null, string? upstreamProxyPassword = null )
    {
        if (upstreamProxyUserName != null && upstreamProxyPassword != null)
        {
            WriteHeader(HttpHeader.ProxyConnectionKeepAlive);
            WriteHeader(HttpHeader.GetProxyAuthorizationHeader(upstreamProxyUserName, upstreamProxyPassword));
        }

        foreach (var header in headers)
            if (sendProxyAuthorization || !KnownHeaders.ProxyAuthorization.Equals(header.Name))
                WriteHeader(header);

        WriteLine();
    }

    public void WriteHeader ( HttpHeader header )
    {
        // "{header.Name}: {header.Value}";
        Write($"{header.Name}: {header.Value}");
        WriteLine();
    }

    public void WriteLine ()
    {
        var data = ProxyConstants.NewLineBytes;
        stream.Write(data, 0, data.Length);
    }

    public void Write ( string str )
    {
        var encoding = HttpHeader.Encoding;

        // Rent a buffer large enough to hold the encoded string
        var maxByteCount = encoding.GetMaxByteCount(str.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);

        try
        {
            // Encode the string into the buffer
            int byteCount = encoding.GetBytes(str, 0, str.Length, buffer, 0);

            // Create a ReadOnlySpan<byte> from the encoded bytes
            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer, 0, byteCount);

            // Write the ReadOnlySpan<byte> to the stream
            stream.Write(span);
        }
        finally
        {
            // Return the buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }


    }

    public ArraySegment<byte> GetBuffer ()
    {
        stream.TryGetBuffer(out var buffer);
        return buffer;
    }

    public string GetString ( Encoding encoding )
    {
        stream.TryGetBuffer(out var buffer);
        return encoding.GetString(buffer.Array!, buffer.Offset, buffer.Count);
    }
}
