using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Helpers;

namespace Unobtanium.Web.Proxy.Extensions;
internal static class HttpClientStreamExtensions
{
    internal static readonly Encoding DefaultEncoding = Encoding.GetEncoding("ISO-8859-1");
    internal static readonly byte[] NewLineBytes = [(byte)'\r', (byte)'\n'];
    public static void WriteLineEncoded(this HttpClientStream stream, string value)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (value == null) throw new ArgumentNullException(nameof(value));

        stream.Write(DefaultEncoding.GetBytes(value).AsSpan());
        stream.WriteLineEnding();
    }

    public static void WriteLineEnding(this HttpClientStream stream)
    {
        stream.Write(NewLineBytes.AsSpan());
    }
}
