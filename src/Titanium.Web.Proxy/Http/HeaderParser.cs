using System;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.StreamExtended.Network;

namespace Titanium.Web.Proxy.Http;

internal static class HeaderParser
{
    /// <summary>
    /// Read all headers from the stream, throws <see cref="FormatException"/> if header is invalid.
    /// </summary>
    /// <param name="reader">Stream containing the data</param>
    /// <param name="headerCollection">Header collection to write to</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    internal static async ValueTask ReadHeaders ( ILineStream reader, HeaderCollection headerCollection,
        CancellationToken cancellationToken )
    {
        string? tmpLine;
        while (!string.IsNullOrEmpty(tmpLine = await reader.ReadLineAsync(cancellationToken)))
        {
            var colonIndex = tmpLine!.IndexOf(':');
            if (colonIndex == -1) throw new FormatException("Header line should contain a colon character.");

            var headerName = tmpLine.AsSpan(0, colonIndex).ToString();
            var headerValue = tmpLine.AsSpan(colonIndex + 1).TrimStart().ToString();
            headerCollection.AddHeader(headerName, headerValue);
        }
    }
}
