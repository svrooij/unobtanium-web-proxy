using System;
using Titanium.Web.Proxy.Models;

namespace Titanium.Web.Proxy.Extensions;

internal static class HttpHeaderExtensions
{
    /// <summary>
    /// Convert byte string to string using the default encoding specified in the <see cref="HttpHeader.Encoding"/>
    /// </summary>
    /// <param name="str"></param>
    internal static string GetString ( this ByteString str )
    {
        return GetString(str.Span);
    }

    /// <summary>
    /// Convert ReadOnlySpan to string using the default encoding specified in the <see cref="HttpHeader.Encoding"/>
    /// </summary>
    /// <param name="bytes"></param>
    internal static string GetString ( this ReadOnlySpan<byte> bytes )
    {
        return HttpHeader.Encoding.GetString(bytes);
    }

    /// <summary>
    /// Convert string to <see cref="ByteString"/> using the default encoding specified in the <see cref="HttpHeader.Encoding"/>
    /// </summary>
    /// <param name="str">Input string</param>
    /// <returns></returns>
    internal static ByteString GetByteString ( this string str )
    {
        return HttpHeader.Encoding.GetBytes(str);
    }
}
