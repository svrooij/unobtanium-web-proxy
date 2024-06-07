using System;
using System.ComponentModel;
using Titanium.Web.Proxy.Exceptions;
using Titanium.Web.Proxy.Extensions;
using Titanium.Web.Proxy.Models;

namespace Titanium.Web.Proxy.Http;

/// <summary>
///     Http(s) response object
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public class Response : RequestResponseBase
{
    /// <summary>
    ///     Constructor.
    /// </summary>
    public Response()
    {
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="body">The response body as a byte array.</param>
    public Response(byte[] body)
    {
        Body = body;
    }

    /// <summary>
    ///     Gets or sets the response status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    ///     Gets or sets the response status description.
    /// </summary>
    public string StatusDescription { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the request method associated with this response.
    /// </summary>
    internal string? RequestMethod { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the response has a body.
    /// </summary>
    public override bool HasBody
    {
        get
        {
            if (RequestMethod == "HEAD") return false;

            var contentLength = ContentLength;

            // If content length is set to 0 the response has no body
            if (contentLength == 0) return false;

            // Has body only if response is chunked or content length >0
            // If none are true then check if connection:close header exist, if so write response until server or client terminates the connection
            if (IsChunked || contentLength > 0 || !KeepAlive) return true;

            if (ContentLength == -1 && HttpVersion == HttpHeader.Version20) return true;

            // has response if connection:keep-alive header exist and when version is http/1.0
            // Because in Http 1.0 server can return a response without content-length (expectation being client would read until end of stream)
            if (KeepAlive && HttpVersion == HttpHeader.Version10) return true;

            return false;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether to keep the connection alive.
    /// </summary>
    public bool KeepAlive
    {
        get
        {
            var headerValue = Headers.GetHeaderValueOrNull(KnownHeaders.Connection);

            if (headerValue != null)
                if (headerValue.EqualsIgnoreCase(KnownHeaders.ConnectionClose.String))
                    return false;

            return true;
        }
    }

    /// <summary>
    ///     Gets the header text of the response.
    /// </summary>
    public override string HeaderText
    {
        get
        {
            var headerBuilder = new HeaderBuilder();
            headerBuilder.WriteResponseLine(HttpVersion, StatusCode, StatusDescription);
            headerBuilder.WriteHeaders(Headers);
            return headerBuilder.GetString(HttpHeader.Encoding);
        }
    }

    /// <summary>
    ///     Ensures the response body is available.
    /// </summary>
    /// <param name="throwWhenNotReadYet">If true, throws an exception if the body is not read yet.</param>
    internal override void EnsureBodyAvailable(bool throwWhenNotReadYet = true)
    {
        if (BodyInternal != null) return;

        if (!HasBody) throw new BodyNotFoundException("Response has no body.");

        if (!IsBodyRead && throwWhenNotReadYet)
            throw new BodyNotLoadedException();
    }

    /// <summary>
    ///     Parses the response line.
    /// </summary>
    /// <param name="httpStatus">The HTTP status line.</param>
    /// <param name="version">The HTTP version.</param>
    /// <param name="statusCode">The status code.</param>
    /// <param name="statusDescription">The status description.</param>
    internal static void ParseResponseLine(string httpStatus, out Version version, out int statusCode,
        out string statusDescription)
    {
        var firstSpace = httpStatus.IndexOf(' ');
        if (firstSpace == -1) throw new Exception("Invalid HTTP status line: " + httpStatus);

        var httpVersion = httpStatus.AsSpan(0, firstSpace);

        version = HttpHeader.Version11;
        if (httpVersion.EqualsIgnoreCase("HTTP/1.0".AsSpan())) version = HttpHeader.Version10;

        var secondSpace = httpStatus.IndexOf(' ', firstSpace + 1);
        if (secondSpace != -1)
        {
#if NET6_0_OR_GREATER
            statusCode = int.Parse(httpStatus.AsSpan(firstSpace + 1, secondSpace - firstSpace - 1));
#else
            statusCode = int.Parse(httpStatus.AsSpan(firstSpace + 1, secondSpace - firstSpace - 1).ToString());
#endif
            statusDescription = httpStatus.AsSpan(secondSpace + 1).ToString();
        }
        else
        {
#if NET6_0_OR_GREATER
            statusCode = int.Parse(httpStatus.AsSpan(firstSpace + 1));
#else
            statusCode = int.Parse(httpStatus.AsSpan(firstSpace + 1).ToString());
#endif
            statusDescription = string.Empty;
        }
    }
}
