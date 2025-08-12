using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Proxy.Extensions;
internal static class StreamExtensions
{
    private const int ReadBufferSize = 8192;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);
    private static readonly System.Text.Encoding DefaultEncoding = System.Text.Encoding.GetEncoding("ISO-8859-1");

    internal static async Task<string?> ReadFirstLineAsync(this NetworkStream stream, System.Threading.CancellationToken cancellationToken = default)
    {
        if (stream is null || !stream.CanRead)
        {
            return null;
        }
        
        using var activity = ProxyServer.ProxyActivitySource.StartActivity("StreamExtensions.ReadFirstLineAsync", System.Diagnostics.ActivityKind.Internal);
        
        // Create a timeout cancellation token source
        using var timeoutCts = new System.Threading.CancellationTokenSource(ReadTimeout);
        using var combinedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        try
        {
            var buffer = new byte[ReadBufferSize];
            var bytesRead = 0;
            var position = 0;
            
            // Read until we find CRLF or reach end of buffer
            while (position < buffer.Length)
            {
                if (!stream.DataAvailable)
                {
                    // No data available yet, wait a bit to avoid busy-waiting
                    await Task.Delay(10, combinedCts.Token);
                    continue;
                }
                
                bytesRead = await stream.ReadAsync(buffer.AsMemory(position, 1), combinedCts.Token);
                if (bytesRead == 0) 
                    break; // End of stream
                
                // Check for line ending (CRLF: \r\n)
                if (position > 0 && buffer[position] == '\n' && buffer[position - 1] == '\r')
                {
                    // Found a line ending
                    return Encoding.ASCII.GetString(buffer, 0, position - 1); // Return without CR+LF
                }
                
                position++;
            }
            
            // If we didn't find a CRLF but read some data, return what we've got
            if (position > 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, position);
            }
            
            return null;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            activity?.SetTag("timeout", "true");
            return null;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    internal static async Task<HttpRequestMessage?> ParseHttpRequestMessageAsync ( this NetworkStream networkStream, CancellationToken cancellationToken)
    {
        var responseLine = await networkStream.ReadFirstLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(responseLine))
        {
            return null; // No response line read
        }
        return await ParseHttpRequestMessageAsync(networkStream, responseLine, cancellationToken);
    }

    internal static async Task<HttpRequestMessage?> ParseHttpRequestMessageAsync(this NetworkStream networkStream, string requestLine, System.Threading.CancellationToken cancellationToken = default)
    {
        if (networkStream is null || !networkStream.CanRead)
        {
            return null;
        }
        
        using var activity = ProxyServer.ProxyActivitySource.StartActivity("StreamExtensions.ParseHttpRequestMessageAsync", System.Diagnostics.ActivityKind.Internal);
        
        try
        {
            if (string.IsNullOrEmpty(requestLine))
                return null;
                
            // Parse the request line: METHOD URL HTTP/VERSION
            var parts = requestLine.Split(' ');
            if (parts.Length < 3)
                return null;
                
            var method = parts[0];
            var url = parts[1];
            var httpVersionPart = parts[2];
            
            // Parse HTTP version
            var httpVersion = ParseHttpVersion(httpVersionPart);
            
            // Parse the URL (ensure it's absolute)
            Uri? requestUri = null;
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                requestUri = new Uri(url);
            }
            
            // Create the HttpRequestMessage
            var httpMethod = new HttpMethod(method);
            var request = new HttpRequestMessage(httpMethod, requestUri ?? new Uri(url, UriKind.RelativeOrAbsolute));
            request.Version = httpVersion;

            // Read HTTP headers
            var headers = await ReadHttpHeadersAsync(networkStream, cancellationToken);
            if (headers == null)
            {
                activity?.SetTag("headers_read", "failed");
                return null;
            }
            
            // Extract important header values
            var contentLength = 0L;
            var transferEncodingChunked = false;
            var host = string.Empty;
            
            foreach (var header in headers)
            {
                var headerName = header.Key;
                var headerValue = header.Value;

                if (headerName.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip Proxy-Connection header as it is not standard
                    continue;
                }

                // Track special headers
                if (headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && 
                    long.TryParse(headerValue, out var length))
                {
                    contentLength = length;
                }
                else if (headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) && 
                         headerValue.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                {
                    transferEncodingChunked = true;
                }
                else if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    host = headerValue;
                }
                
                // Set header on request
                try
                {
                    request.Headers.TryAddWithoutValidation(headerName, headerValue);
                }
                catch
                {
                    // Invalid header, skip it
                }
            }
            
            // If Host header is present and we don't have an absolute URI, construct one
            if (!string.IsNullOrEmpty(host) && !Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                var scheme = "http"; // Default scheme
                var fullUrl = $"{scheme}://{host}{url}";
                if (Uri.IsWellFormedUriString(fullUrl, UriKind.Absolute))
                {
                    request.RequestUri = new Uri(fullUrl);
                }
            }

            // Parse the body if it exists
            if (contentLength > 0 || transferEncodingChunked)
            {
                byte[] bodyContent;
                
                if (transferEncodingChunked)
                {
                    bodyContent = await ReadChunkedBodyAsync(networkStream, cancellationToken);
                }
                else if (contentLength > 0)
                {
                    bodyContent = await ReadBodyAsync(networkStream, contentLength, cancellationToken);
                }
                else
                {
                    bodyContent = Array.Empty<byte>();
                }
                
                if (bodyContent != null && bodyContent.Length > 0)
                {
                    request.Content = new ByteArrayContent(bodyContent);
                    
                    // Apply content headers
                    foreach (var header in headers)
                    {
                        if (!IsContentHeader(header.Key))
                            continue;
                            
                        try
                        {
                            // Remove from request headers if it's a content header
                            if (request.Headers.Contains(header.Key))
                            {
                                request.Headers.Remove(header.Key);
                            }
                            
                            request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                        catch
                        {
                            // Ignore invalid content headers
                        }
                    }
                }
            }
            
            activity?.SetTag("request_parsed", "success");
            return request;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }
    
    private static async Task<Dictionary<string, string>?> ReadHttpHeadersAsync(NetworkStream networkStream, System.Threading.CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Create a timeout token
        using var timeoutCts = new System.Threading.CancellationTokenSource(ReadTimeout);
        using var combinedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        try
        {
            string? line;
            // Read headers until empty line
            while (!string.IsNullOrEmpty(line = await ReadLineWithTimeoutAsync(networkStream, combinedCts.Token)))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) 
                    continue;
                
                var headerName = line.Substring(0, colonIndex).Trim();
                var headerValue = line.Substring(colonIndex + 1).Trim();
                
                // Headers can be repeated, we'll just overwrite for simplicity
                headers[headerName] = headerValue;
            }
            
            return headers;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout reading headers
            return null;
        }
    }
    
    private static async Task<string?> ReadLineWithTimeoutAsync(NetworkStream networkStream, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReadBufferSize];
        var position = 0;
        
        while (position < buffer.Length)
        {
            // If there's no data available, wait for it or timeout
            if (!networkStream.DataAvailable)
            {
                await Task.Delay(10, cancellationToken);
                // If we've already tried and still no data, check for timeout
                if (!networkStream.DataAvailable)
                {
                    // Check if we have partial data
                    if (position > 0)
                    {
                        return Encoding.ASCII.GetString(buffer, 0, position);
                    }
                    continue;
                }
            }
            
            // Read a single byte
            var bytesRead = await networkStream.ReadAsync(buffer.AsMemory(position, 1), cancellationToken);
            if (bytesRead == 0)
                break; // End of stream
                
            // Check for line ending (CRLF)
            if (position > 0 && buffer[position] == '\n' && buffer[position - 1] == '\r')
            {
                // Found end of line, return the line without CRLF
                return Encoding.ASCII.GetString(buffer, 0, position - 1);
            }
            
            position++;
        }
        
        // If we get here without finding CRLF but have data, return what we have
        if (position > 0)
        {
            return Encoding.ASCII.GetString(buffer, 0, position);
        }
        
        return null;
    }
    
    private static Version ParseHttpVersion(string versionString)
    {
        if (versionString.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
        {
            var versionPart = versionString.Substring(5);
            if (Version.TryParse(versionPart, out var version))
            {
                return version;
            }
        }
        
        return new Version(1, 1); // Default to HTTP/1.1
    }
    
    private static bool IsContentHeader(string headerName)
    {
        // Common content headers
        return headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Language", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Location", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Range", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-MD5", StringComparison.OrdinalIgnoreCase);
    }
    
    private static async Task<byte[]> ReadBodyAsync(NetworkStream networkStream, long contentLength, System.Threading.CancellationToken cancellationToken)
    {
        // For large content, we might want to stream directly instead of buffering everything
        if (contentLength > 100 * 1024 * 1024) // 100MB limit
        {
            throw new InvalidOperationException("Content is too large to buffer");
        }
        
        using var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30)); // Longer timeout for body
        using var combinedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var buffer = new byte[contentLength];
        var bytesRead = 0;
        var totalBytesRead = 0L;
        
        while (totalBytesRead < contentLength)
        {
            bytesRead = await networkStream.ReadAsync(
                buffer.AsMemory((int)totalBytesRead, (int)Math.Min(ReadBufferSize, contentLength - totalBytesRead)), 
                combinedCts.Token);
                
            if (bytesRead == 0)
                break;
                
            totalBytesRead += bytesRead;
        }
        
        // If we couldn't read the full body, return what we got
        if (totalBytesRead < contentLength)
        {
            var partialBuffer = new byte[totalBytesRead];
            Array.Copy(buffer, partialBuffer, totalBytesRead);
            return partialBuffer;
        }
        
        return buffer;
    }
    
    private static async Task<byte[]> ReadChunkedBodyAsync(NetworkStream networkStream, System.Threading.CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        using var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30)); // Longer timeout for body
        using var combinedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        while (true)
        {
            // Read chunk size line
            var chunkSizeLine = await ReadLineWithTimeoutAsync(networkStream, combinedCts.Token);
            if (string.IsNullOrEmpty(chunkSizeLine))
                break;
            
            // Parse chunk size (hexadecimal)
            int chunkSize;
            try
            {
                // Remove chunk extensions if present
                var semicolonIndex = chunkSizeLine.IndexOf(';');
                var hexString = semicolonIndex >= 0 
                    ? chunkSizeLine.Substring(0, semicolonIndex).Trim() 
                    : chunkSizeLine.Trim();
                    
                chunkSize = Convert.ToInt32(hexString, 16);
            }
            catch
            {
                break; // Invalid chunk size
            }
            
            if (chunkSize == 0)
                break; // End of chunks
                
            // Read chunk data
            var chunkBuffer = new byte[chunkSize];
            int totalRead = 0;
            
            while (totalRead < chunkSize)
            {
                var bytesToRead = Math.Min(chunkSize - totalRead, ReadBufferSize);
                var bytesRead = await networkStream.ReadAsync(
                    chunkBuffer.AsMemory(totalRead, bytesToRead), combinedCts.Token);
                    
                if (bytesRead == 0)
                    break; // Unexpected end of data
                    
                totalRead += bytesRead;
            }
            
            if (totalRead > 0)
            {
                await memoryStream.WriteAsync(chunkBuffer.AsMemory(0, totalRead), combinedCts.Token);
            }
            
            // Read and discard the trailing CRLF
            await ReadLineWithTimeoutAsync(networkStream, combinedCts.Token);
        }
        
        // Read trailing headers (ignored for simplicity)
        string? trailerLine;
        while (!string.IsNullOrEmpty(trailerLine = await ReadLineWithTimeoutAsync(networkStream, combinedCts.Token)))
        {
            // Process trailing headers if needed
        }
        
        return memoryStream.ToArray();
    }
}
