﻿using System;
using System.ComponentModel;
using Unobtanium.Web.Proxy.Exceptions;
using Unobtanium.Web.Proxy.Extensions;
using Unobtanium.Web.Proxy.Models;

namespace Unobtanium.Web.Proxy.Http;

/// <summary>
///     Http(s) request object.
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public class Request : RequestResponseBase
{
    private ByteString requestUriString8;

    /// <summary>
    ///     Gets or sets the request method.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the request is over HTTPS.
    /// </summary>
    public bool IsHttps { get; internal set; }

    internal ByteString RequestUriString8
    {
        get => requestUriString8;
        set
        {
            requestUriString8 = value;
            var scheme = UriExtensions.GetScheme(value);
            if (scheme.Length > 0) IsHttps = scheme.Equals(ProxyServer.UriSchemeHttps8);
        }
    }

    internal ByteString Authority { get; set; }

    /// <summary>
    ///     Gets or sets the request URI.
    /// </summary>
    public Uri RequestUri
    {
        get
        {
            var url = Url;
            try
            {
                return new Uri(url);
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid URI: '{url}'", ex);
            }
        }
        set => Url = value.OriginalString;
    }

    /// <summary>
    ///     Gets or sets the request URL as it appears in the HTTP header.
    /// </summary>
    public string Url
    {
        get
        {
            var url = RequestUriString8.GetString();
            if (UriExtensions.GetScheme(RequestUriString8).Length == 0)
            {
                var hostAndPath = Host ?? Authority.GetString();

                if (url.StartsWith('/'))
                {
                    hostAndPath += url;
                }

                url = string.Concat(IsHttps ? "https://" : "http://", hostAndPath);
            }

            return url;
        }
        set => RequestUriString = value;
    }

    /// <summary>
    ///     Gets or sets the request URI as a string.
    /// </summary>
    public string RequestUriString
    {
        get => RequestUriString8.GetString();
        set
        {
            RequestUriString8 = (ByteString)value;

            var scheme = UriExtensions.GetScheme(RequestUriString8);
            if (scheme.Length > 0 && Host != null)
            {
                var uri = new Uri(value);
                Host = uri.Authority;
                Authority = ByteString.Empty;
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the request has a body.
    /// </summary>
    public override bool HasBody
    {
        get
        {
            var contentLength = ContentLength;

            // If content length is set to 0 the request has no body
            if (contentLength == 0) return false;

            // Has body only if request is chunked or content length >0
            if (IsChunked || contentLength > 0) return true;

            // has body if POST and when version is http/1.0
            if (Method == "POST" && HttpVersion == HttpHeader.Version10) return true;

            return false;
        }
    }

    /// <summary>
    ///     Gets or sets the Http hostname header value if exists.
    ///     Note: Changing this does NOT change host in RequestUri.
    ///     Users can set new RequestUri separately.
    /// </summary>
    public string? Host
    {
        get => Headers.GetHeaderValueOrNull(KnownHeaders.Host);
        set => Headers.SetOrAddHeaderValue(KnownHeaders.Host, value);
    }

    /// <summary>
    ///     Gets a value indicating whether the request has a 100-continue header.
    /// </summary>
    public bool ExpectContinue
    {
        get
        {
            var headerValue = Headers.GetHeaderValueOrNull(KnownHeaders.Expect);
            return KnownHeaders.Expect100Continue.Equals(headerValue);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the request contains multipart/form-data.
    /// </summary>
    public bool IsMultipartFormData => ContentType?.StartsWith("multipart/form-data") == true;

    /// <summary>
    ///     Cancels the client HTTP request without sending to server.
    ///     This should be set when API user responds with custom response.
    /// </summary>
    internal bool CancelRequest { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the request has an upgrade to websocket header.
    /// </summary>
    public bool UpgradeToWebSocket
    {
        get
        {
            var headerValue = Headers.GetHeaderValueOrNull(KnownHeaders.Upgrade);

            if (headerValue == null) return false;

            return headerValue.EqualsIgnoreCase(KnownHeaders.UpgradeWebsocket.String);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the server responded positively for 100 continue request.
    /// </summary>
    public bool ExpectationSucceeded { get; internal set; }

    /// <summary>
    ///     Gets a value indicating whether the server responded negatively for 100 continue request.
    /// </summary>
    public bool ExpectationFailed { get; internal set; }

    /// <summary>
    ///     Gets the header text.
    /// </summary>
    public override string HeaderText
    {
        get
        {
            var headerBuilder = new HeaderBuilder();
            headerBuilder.WriteRequestLine(Method!, RequestUriString, HttpVersion);
            headerBuilder.WriteHeaders(Headers);
            return headerBuilder.GetString(HttpHeader.Encoding);
        }
    }

    internal override void EnsureBodyAvailable ( bool throwWhenNotReadYet = true )
    {
        if (BodyInternal != null) return;

        // GET request don't have a request body to read
        if (!HasBody)
            throw new BodyNotFoundException();

        if (!IsBodyRead)
        {
            if (Locked) throw new BodyLockedException();

            if (throwWhenNotReadYet)
                throw new BodyNotLoadedException();
        }
    }

    internal static void ParseRequestLine ( string httpCmd, out string method, out ByteString requestUri,
        out Version version )
    {
        var firstSpace = httpCmd.IndexOf(' ');
        if (firstSpace == -1)
            // does not contain at least 2 parts
            throw new Exception("Invalid HTTP request line: " + httpCmd);

        var lastSpace = httpCmd.LastIndexOf(' ');

        // break up the line into three components (method, remote URL & Http Version)

        // Find the request Verb
        method = httpCmd[..firstSpace];
        if (!IsAllUpper(method)) method = method.ToUpper();

        version = HttpHeader.Version11;

        if (firstSpace == lastSpace)
        {
            requestUri = (ByteString)httpCmd.AsSpan(firstSpace + 1).ToString();
        }
        else
        {
            requestUri = (ByteString)httpCmd.AsSpan(firstSpace + 1, lastSpace - firstSpace - 1).ToString();

            // parse the HTTP version
            var httpVersion = httpCmd.AsSpan(lastSpace + 1);

            if (httpVersion.EqualsIgnoreCase("HTTP/1.0".AsSpan(0))) version = HttpHeader.Version10;
        }
    }

    private static bool IsAllUpper ( string input )
    {
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch < 'A' || ch > 'Z') return false;
        }

        return true;
    }
}
