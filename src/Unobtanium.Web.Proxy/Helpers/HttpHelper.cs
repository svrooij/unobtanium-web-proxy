using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Extensions;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Models;
using Unobtanium.Web.Proxy.Shared;
using Unobtanium.Web.Proxy.StreamExtended.BufferPool;
using Unobtanium.Web.Proxy.StreamExtended.Network;

namespace Unobtanium.Web.Proxy.Helpers;

internal static class HttpHelper
{
    /// <summary>
    ///     Gets the character encoding of request/response from content-type header
    /// </summary>
    /// <param name="contentType"></param>
    /// <returns></returns>
    internal static Encoding GetEncodingFromContentType ( string? contentType )
    {
        if (string.IsNullOrEmpty(contentType)) return HttpHeader.DefaultEncoding;

        try
        {
            var charsetPrefix = "charset=";
            var startIndex = contentType.IndexOf(charsetPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex != -1)
            {
                startIndex += charsetPrefix.Length;
                var endIndex = contentType.IndexOf(';', startIndex);
                endIndex = endIndex == -1 ? contentType.Length : endIndex;
                var charsetValue = contentType[startIndex..endIndex].Trim();

                if (charsetValue.StartsWith('"') && charsetValue.EndsWith('"') && charsetValue.Length > 2)
                {
                    charsetValue = charsetValue[1..^1];
                }

                if (!charsetValue.Equals("x-user-defined", StringComparison.OrdinalIgnoreCase))
                {
                    return Encoding.GetEncoding(charsetValue);
                }
            }
        }
        catch
        {
            // Ignored
        }

        return HttpHeader.DefaultEncoding;
    }

    internal static ReadOnlyMemory<char> GetBoundaryFromContentType ( string? contentType )
    {
        if (contentType is not null)
            // extract the boundary
            foreach (var parameter in new SemicolonSplitEnumerator(contentType))
            {
                var equalsIndex = parameter.Span.IndexOf('=');
                if (equalsIndex != -1 &&
                    KnownHeaders.ContentTypeBoundary.Equals(parameter.Span[..equalsIndex].TrimStart()))
                {
                    var value = parameter[(equalsIndex + 1)..];
                    if (value.Length > 2 && value.Span[0] == '"' && value.Span[value.Length - 1] == '"')
                        value = value[1..^1];

                    return value;
                }
            }

        // return null if not specified
        return null;
    }

    /// <summary>
    ///     Tries to get root domain from a given hostname
    ///     Adapted from below answer
    ///     https://stackoverflow.com/questions/16473838/get-domain-name-of-a-url-in-c-sharp-net
    /// </summary>
    /// <param name="hostname"></param>
    /// <param name="disableWildCardCertificates"></param>
    /// <returns></returns>
    internal static string GetWildCardDomainName ( string hostname, bool disableWildCardCertificates )
    {
        // only for subdomains we need wild card
        // example www.google.com or gstatic.google.com
        // but NOT for google.com or IP address

        if (IPAddress.TryParse(hostname, out _)) return hostname;

        if (disableWildCardCertificates) return hostname;

        var split = hostname.Split(ProxyConstants.DotSplit);

        if (split.Length > 2)
        {
            // issue #769
            // do not create wildcard if second level domain like: pay.vn.ua
            if (split[0] != "www" && split[1].Length <= 3) return hostname;

            var idx = hostname.IndexOf(ProxyConstants.DotSplit);

            // issue #352
            if (hostname[..idx].Contains('-')) return hostname;

            var rootDomain = hostname[(idx + 1)..];
            return "*." + rootDomain;
        }

        // return as it is
        return hostname;
    }

    /// <summary>
    ///     Gets the HTTP method from the stream.
    /// </summary>
    public static async ValueTask<KnownMethod> GetMethod ( IPeekStream httpReader, IBufferPool bufferPool, CancellationToken cancellationToken = default )
    {
        const int lengthToCheck = 20;
        if (bufferPool.BufferSize < lengthToCheck)
            throw new Exception($"Buffer is too small. Minimum size is {lengthToCheck} bytes");

        // TODO: Getting a large buffer for only reading the method seems wrong, need fix!
        var buffer = bufferPool.GetBuffer(bufferPool.BufferSize);
        try
        {
            // Attempt to read the maximum expected length in one go to reduce await overhead.
            var peeked = await httpReader.PeekBytesAsync(buffer, 0, 0, lengthToCheck, cancellationToken);
            if (peeked <= 0)
                return KnownMethod.Invalid;

            // Find the first space character indicating the end of the HTTP method.
            int methodLength = Array.IndexOf(buffer, (byte)' ', 0, peeked);
            if (methodLength > 2) // Minimum length for an HTTP method is 3.
            {
                return GetKnownMethod(buffer.AsSpan(0, methodLength));
            }
            else
            {
                // If the method is too short or no space was found in the peeked bytes, it's invalid.
                return KnownMethod.Invalid;
            }
        }
        finally
        {
            bufferPool.ReturnBuffer(buffer);
        }
    }


    internal static KnownMethod GetKnownMethod ( ReadOnlySpan<byte> method )
    {
        if (method.Length < 3) return KnownMethod.Unknown;

        switch (method.Length)
        {
            case 3:
                if (method[0] == 'G' && method[1] == 'E' && method[2] == 'T') return KnownMethod.Get;
                if (method[0] == 'P' && method[1] == 'U' && method[2] == 'T') return KnownMethod.Put;
                if (method[0] == 'P' && method[1] == 'R' && method[2] == 'I') return KnownMethod.Pri;
                break;
            case 4:
                if (method[0] == 'H' && method[1] == 'E' && method[2] == 'A' && method[3] == 'D') return KnownMethod.Head;
                if (method[0] == 'P' && method[1] == 'O' && method[2] == 'S' && method[3] == 'T') return KnownMethod.Post;
                break;
            case 5:
                if (method[0] == 'T' && method[1] == 'R' && method[2] == 'A' && method[3] == 'C' && method[4] == 'E') return KnownMethod.Trace;
                break;
            case 6:
                if (method[0] == 'D' && method[1] == 'E' && method[2] == 'L' && method[3] == 'E' && method[4] == 'T' && method[5] == 'E') return KnownMethod.Delete;
                break;
            case 7:
                if (method[0] == 'C' && method[1] == 'O' && method[2] == 'N' && method[3] == 'N' && method[4] == 'E' && method[5] == 'C' && method[6] == 'T') return KnownMethod.Connect;
                if (method[0] == 'O' && method[1] == 'P' && method[2] == 'T' && method[3] == 'I' && method[4] == 'O' && method[5] == 'N' && method[6] == 'S') return KnownMethod.Options;
                break;
        }

        return KnownMethod.Unknown;
    }

    private struct SemicolonSplitEnumerator ( ReadOnlyMemory<char> data )
    {
        private int idx = 0;

        public SemicolonSplitEnumerator ( string str ) : this(str.AsMemory())
        {
        }

        public readonly SemicolonSplitEnumerator GetEnumerator () => this;

        public bool MoveNext ()
        {
            if (this.idx > data.Length) return false;

            var idx = data.Span[this.idx..].IndexOf(';');
            if (idx == -1)
                idx = data.Length;
            else
                idx += this.idx;

            Current = data[this.idx..idx];
            this.idx = idx + 1;
            return true;
        }


        public ReadOnlyMemory<char> Current { get; private set; } = null;
    }
}
