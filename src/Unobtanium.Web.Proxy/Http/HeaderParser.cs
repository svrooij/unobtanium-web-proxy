using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.StreamExtended.Network;

namespace Unobtanium.Web.Proxy.Http;

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

    /// <summary>
    /// Read all headers from the stream directly into HttpRequestMessage, throws <see cref="FormatException"/> if header is invalid.
    /// This method eliminates the intermediate HeaderCollection step for better performance.
    /// </summary>
    /// <param name="reader">Stream containing the data</param>
    /// <param name="httpRequest">HttpRequestMessage to add headers to</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Returns a HeaderCollection containing any content headers that couldn't be added to the request headers</returns>
    /// <exception cref="FormatException"></exception>
    internal static async ValueTask<IEnumerable<KeyValuePair<string,string>>> ReadHeadersDirectly(ILineStream reader, HttpRequestMessage httpRequest,
        CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>>? contentHeaders = [];
        string? tmpLine;
        
        while (!string.IsNullOrEmpty(tmpLine = await reader.ReadLineAsync(cancellationToken)))
        {
            var colonIndex = tmpLine!.IndexOf(':');
            if (colonIndex == -1) throw new FormatException("Header line should contain a colon character.");

            var headerName = tmpLine.AsSpan(0, colonIndex).ToString();
            var headerValue = tmpLine.AsSpan(colonIndex + 1).TrimStart().ToString();

            // Skip compression-related headers to prevent compressed responses
            // This ensures the proxy can easily process and modify response content
            if (headerName.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                continue; // Skip this header completely
            }

            try
            {
                // Try to add to request headers first
                if (!httpRequest.Headers.TryAddWithoutValidation(headerName, headerValue))
                {
                    // If it fails, it might be a content header, store it for later processing
                    contentHeaders.Add(new KeyValuePair<string, string>(headerName, headerValue));
                }
            }
            catch
            {
                // Some headers might not be valid, skip them
            }
        }

        return contentHeaders;
    }
}
