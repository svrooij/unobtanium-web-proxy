using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Http;

/// <summary>
/// High-performance HTTP request parser using System.IO.Pipelines for optimal memory efficiency.
/// This provides significantly better performance than traditional stream-based parsing.
/// </summary>
internal static class PipelineHttpRequestParser
{
    private static readonly byte[] CrLf = { (byte)'\r', (byte)'\n' };
    private static readonly byte[] Lf = { (byte)'\n' };

    /// <summary>
    /// Parse an HTTP request from a Stream using high-performance pipelines
    /// </summary>
    /// <param name="stream">The stream containing HTTP request data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HttpRequestMessage or null if parsing fails</returns>
    internal static async Task<HttpRequestMessage?> ParseRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        var pipeReader = PipeReader.Create(stream);
        
        try
        {
            return await ParseRequestFromPipeAsync(pipeReader, cancellationToken);
        }
        finally
        {
            await pipeReader.CompleteAsync();
        }
    }

    /// <summary>
    /// Parse an HTTP request from a PipeReader using high-performance parsing
    /// </summary>
    /// <param name="pipeReader">The pipe reader containing HTTP request data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HttpRequestMessage or null if parsing fails</returns>
    private static async Task<HttpRequestMessage?> ParseRequestFromPipeAsync(PipeReader pipeReader, CancellationToken cancellationToken)
    {
        try
        {
            // Read request line
            var requestLineResult = await ReadLineAsync(pipeReader, cancellationToken);
            if (requestLineResult == null)
                return null;

            var requestLineParts = ParseRequestLine(requestLineResult);
            if (requestLineParts == null)
                return null;

            var (method, path, version) = requestLineParts.Value;

            // Read headers
            var headers = new List<KeyValuePair<string, string>>();
            var contentHeaders = new List<KeyValuePair<string, string>>();

            while (true)
            {
                var headerLine = await ReadLineAsync(pipeReader, cancellationToken);
                if (string.IsNullOrEmpty(headerLine))
                    break; // Empty line indicates end of headers

                var colonIndex = headerLine.IndexOf(':');
                if (colonIndex == -1)
                    continue; // Invalid header, skip

                // More careful header name and value extraction
                var headerName = headerLine.Substring(0, colonIndex).Trim();
                var headerValue = headerLine.Substring(colonIndex + 1).Trim();

                // Debug: Log header parsing for troubleshooting
                System.Diagnostics.Debug.WriteLine($"Parsed header: '{headerName}': '{headerValue}' (original line: '{headerLine}', colon at: {colonIndex})");

                // Skip Accept-Encoding to prevent compressed responses
                if (headerName.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase))
                    continue;

                headers.Add(new KeyValuePair<string, string>(headerName, headerValue));
            }

            // Create HttpRequestMessage
            var httpMethod = new HttpMethod(method);
            var httpRequest = new HttpRequestMessage(httpMethod, path)
            {
                Version = ParseHttpVersion(version)
            };

            // Add headers and separate content headers
            foreach (var header in headers)
            {
                if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    contentHeaders.Add(header);
                }
            }

            // Build proper URI if needed
            if (httpRequest.RequestUri != null && !httpRequest.RequestUri.IsAbsoluteUri)
            {
                var hostHeader = GetHeaderValue(headers, "Host");
                if (hostHeader != null)
                {
                    var scheme = "http"; // Will be updated by caller if HTTPS
                    var fullUri = $"{scheme}://{hostHeader}{path}";
                    if (Uri.IsWellFormedUriString(fullUri, UriKind.Absolute))
                    {
                        httpRequest.RequestUri = new Uri(fullUri);
                    }
                }
            }

            // Handle request body
            await ProcessRequestBodyAsync(pipeReader, httpRequest, headers, contentHeaders, cancellationToken);

            return httpRequest;
        }
        catch (Exception ex)
        {
            // Debug: Log parsing exceptions
            System.Diagnostics.Debug.WriteLine($"HTTP parsing failed: {ex.Message}");
            return null;
        }
    }

    private static async Task ProcessRequestBodyAsync(
        PipeReader pipeReader, 
        HttpRequestMessage httpRequest, 
        List<KeyValuePair<string, string>> headers,
        List<KeyValuePair<string, string>> contentHeaders,
        CancellationToken cancellationToken)
    {
        var contentLengthHeader = GetHeaderValue(headers, "Content-Length");
        var transferEncodingHeader = GetHeaderValue(headers, "Transfer-Encoding");
        
        long contentLength = 0;
        var hasContentLength = contentLengthHeader != null && long.TryParse(contentLengthHeader, out contentLength);
        var isChunked = transferEncodingHeader?.Contains("chunked", StringComparison.OrdinalIgnoreCase) == true;

        if ((hasContentLength && contentLength > 0) || isChunked)
        {
            byte[]? bodyData = null;

            if (isChunked)
            {
                bodyData = await ReadChunkedBodyAsync(pipeReader, cancellationToken);
            }
            else if (hasContentLength && contentLength > 0)
            {
                bodyData = await ReadContentLengthBodyAsync(pipeReader, contentLength, cancellationToken);
            }

            if (bodyData != null && bodyData.Length > 0)
            {
                httpRequest.Content = new ByteArrayContent(bodyData);
                
                // Apply content headers
                foreach (var contentHeader in contentHeaders)
                {
                    httpRequest.Content.Headers.TryAddWithoutValidation(contentHeader.Key, contentHeader.Value);
                }
            }
        }
    }

    private static async Task<string?> ReadLineAsync(PipeReader pipeReader, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await pipeReader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            // Look for line ending
            var position = FindLineEnd(buffer, out var lineEndLength);
            
            if (position != null)
            {
                var lineSpan = buffer.Slice(0, position.Value);
                var line = GetStringFromSequence(lineSpan);
                
                // Advance past the line and line ending
                // position.Value points to the start of line ending, so we need to advance past it
                var nextPosition = buffer.GetPosition(lineEndLength, position.Value);
                pipeReader.AdvanceTo(nextPosition);
                
                // Debug: Log line reading
                System.Diagnostics.Debug.WriteLine($"Read line: '{line}' (length: {line.Length})");
                
                return line;
            }

            if (result.IsCompleted)
            {
                // No more data and no line ending found
                if (buffer.Length > 0)
                {
                    var line = GetStringFromSequence(buffer);
                    pipeReader.AdvanceTo(buffer.End);
                    return line;
                }
                return null;
            }

            // Tell the PipeReader how much of the buffer has been consumed
            pipeReader.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    private static SequencePosition? FindLineEnd(ReadOnlySequence<byte> buffer, out int lineEndLength)
    {
        var reader = new SequenceReader<byte>(buffer);
        
        while (reader.TryRead(out var b))
        {
            if (b == '\n')
            {
                lineEndLength = 1;
                // Position is already past the \n, so we need to get the position before it
                return buffer.GetPosition(-1, reader.Position);
            }
            
            if (b == '\r')
            {
                // Save position before the \r
                var crPosition = buffer.GetPosition(-1, reader.Position);
                
                if (reader.TryRead(out var next) && next == '\n')
                {
                    lineEndLength = 2;
                    // Return position before the \r (start of CRLF)
                    return crPosition;
                }
                else
                {
                    // Just \r without \n - we need to rewind the reader since we consumed a byte that wasn't \n
                    reader.Rewind(1);
                    lineEndLength = 1;
                    return crPosition;
                }
            }
        }

        lineEndLength = 0;
        return null;
    }

    private static string GetStringFromSequence(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
        {
            return Encoding.UTF8.GetString(sequence.FirstSpan);
        }

        var length = (int)sequence.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            sequence.CopyTo(buffer);
            return Encoding.UTF8.GetString(buffer, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static (string method, string path, string version)? ParseRequestLine(string requestLine)
    {
        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return null;

        return (parts[0], parts[1], parts[2]);
    }

    private static Version ParseHttpVersion(string version)
    {
        return version switch
        {
            "HTTP/1.0" => new Version(1, 0),
            "HTTP/1.1" => new Version(1, 1),
            "HTTP/2.0" => new Version(2, 0),
            _ => new Version(1, 1)
        };
    }

    private static string? GetHeaderValue(List<KeyValuePair<string, string>> headers, string headerName)
    {
        foreach (var header in headers)
        {
            if (header.Key.Equals(headerName, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }
        return null;
    }

    private static async Task<byte[]> ReadContentLengthBodyAsync(PipeReader pipeReader, long contentLength, CancellationToken cancellationToken)
    {
        var bodyBuffer = new byte[contentLength];
        var totalRead = 0L;

        while (totalRead < contentLength)
        {
            var result = await pipeReader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            var remaining = contentLength - totalRead;
            var toRead = Math.Min(buffer.Length, remaining);
            
            var sequence = buffer.Slice(0, toRead);
            sequence.CopyTo(bodyBuffer.AsSpan((int)totalRead));
            
            totalRead += toRead;
            pipeReader.AdvanceTo(buffer.GetPosition(toRead));

            if (result.IsCompleted && totalRead < contentLength)
            {
                break;
            }
        }

        return bodyBuffer;
    }

    private static async Task<byte[]> ReadChunkedBodyAsync(PipeReader pipeReader, CancellationToken cancellationToken)
    {
        var chunks = new List<byte[]>();
        var totalSize = 0;

        while (true)
        {
            // Read chunk size line
            var chunkSizeLine = await ReadLineAsync(pipeReader, cancellationToken);
            if (string.IsNullOrEmpty(chunkSizeLine))
                break;

            // Parse chunk size (hex)
            var chunkSizeString = chunkSizeLine.Split(';')[0];
            if (!int.TryParse(chunkSizeString, System.Globalization.NumberStyles.HexNumber, null, out var chunkSize))
                break;

            if (chunkSize == 0)
            {
                // Read trailing headers (if any) and break
                while (true)
                {
                    var trailerLine = await ReadLineAsync(pipeReader, cancellationToken);
                    if (string.IsNullOrEmpty(trailerLine))
                        break;
                }
                break;
            }

            // Read chunk data
            var chunkData = await ReadContentLengthBodyAsync(pipeReader, chunkSize, cancellationToken);
            chunks.Add(chunkData);
            totalSize += chunkData.Length;

            // Read trailing CRLF after chunk data
            await ReadLineAsync(pipeReader, cancellationToken);
        }

        // Combine all chunks
        var result = new byte[totalSize];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(result, offset);
            offset += chunk.Length;
        }

        return result;
    }
}
