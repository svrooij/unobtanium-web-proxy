﻿using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.EventArguments;
using Unobtanium.Web.Proxy.Exceptions;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Models;

namespace Unobtanium.Web.Proxy;

public partial class ProxyServer
{
    /// <summary>
    ///     Callback to authorize clients of this proxy instance.
    /// </summary>
    /// <param name="session">The session event arguments.</param>
    /// <returns>True if authorized.</returns>
    private async Task<bool> CheckAuthorization ( SessionEventArgsBase session )
    {
        // If we are not authorizing clients return true
        if (ProxyBasicAuthenticateFunc == null && ProxySchemeAuthenticateFunc == null) return true;

        var httpHeaders = session.HttpClient.Request.Headers;

        try
        {
            var headerObj = httpHeaders.GetFirstHeader(KnownHeaders.ProxyAuthorization);
            if (headerObj == null)
            {
                session.HttpClient.Response = CreateAuthentication407Response("Proxy Authentication Required");
                return false;
            }

            var header = headerObj.Value;
            var firstSpace = header.IndexOf(' ');

            // header value should contain exactly 1 space
            if (firstSpace == -1 || header.IndexOf(' ', firstSpace + 1) != -1)
            {
                // Return not authorized
                session.HttpClient.Response = CreateAuthentication407Response("Proxy Authentication Invalid");
                return false;
            }

            var authenticationType = header.AsMemory(0, firstSpace);
            var credentials = header.AsMemory(firstSpace + 1);

            if (ProxyBasicAuthenticateFunc != null)
                return await AuthenticateUserBasic(session, authenticationType, credentials,
                    ProxyBasicAuthenticateFunc);

            if (ProxySchemeAuthenticateFunc != null)
            {
                var result =
                    await ProxySchemeAuthenticateFunc(session, authenticationType.ToString(), credentials.ToString());

                if (result.Result == ProxyAuthenticationResult.ContinuationNeeded)
                {
                    session.HttpClient.Response =
                        CreateAuthentication407Response("Proxy Authentication Invalid", result.Continuation);

                    return false;
                }

                return result.Result == ProxyAuthenticationResult.Success;
            }

            return false;
        }
        catch (Exception e)
        {
            OnException(null, new ProxyAuthorizationException("Error whilst authorizing request", session, e,
                httpHeaders));

            // Return not authorized
            session.HttpClient.Response = CreateAuthentication407Response("Proxy Authentication Invalid");
            return false;
        }
    }

    private async Task<bool> AuthenticateUserBasic ( SessionEventArgsBase session,
        ReadOnlyMemory<char> authenticationType, ReadOnlyMemory<char> credentials,
        Func<SessionEventArgsBase, string, string, Task<bool>> proxyBasicAuthenticateFunc )
    {
        if (!KnownHeaders.ProxyAuthorizationBasic.Equals(authenticationType.Span))
        {
            // Return not authorized
            session.HttpClient.Response = CreateAuthentication407Response("Proxy Authentication Invalid");
            return false;
        }

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(credentials.ToString()));
        var colonIndex = decoded.IndexOf(':');
        if (colonIndex == -1)
        {
            // Return not authorized
            session.HttpClient.Response = CreateAuthentication407Response("Proxy Authentication Invalid");
            return false;
        }

        var username = decoded[..colonIndex];
        var password = decoded[(colonIndex + 1)..];
        var authenticated = await proxyBasicAuthenticateFunc(session, username, password);
        if (!authenticated)
            session.HttpClient.Response = CreateAuthentication407Response("Proxy Authentication Invalid");

        return authenticated;
    }

    /// <summary>
    ///     Create an authentication required response.
    /// </summary>
    /// <param name="description">Response description.</param>
    /// <param name="continuation">The continuation.</param>
    /// <returns></returns>
    private Response CreateAuthentication407Response ( string description, string? continuation = null )
    {
        var response = new Response
        {
            HttpVersion = HttpHeader.Version11,
            StatusCode = (int)HttpStatusCode.ProxyAuthenticationRequired,
            StatusDescription = description
        };

        if (!string.IsNullOrWhiteSpace(continuation)) return CreateContinuationResponse(response, continuation!);

        if (ProxyBasicAuthenticateFunc != null)
            response.Headers.AddHeader(KnownHeaders.ProxyAuthenticate, $"Basic realm=\"{ProxyAuthenticationRealm}\"");

        if (ProxySchemeAuthenticateFunc != null)
            foreach (var scheme in ProxyAuthenticationSchemes)
                response.Headers.AddHeader(KnownHeaders.ProxyAuthenticate, scheme);

        response.Headers.AddHeader(KnownHeaders.ProxyConnection, KnownHeaders.ProxyConnectionClose);

        response.Headers.FixProxyHeaders();
        return response;
    }

    private static Response CreateContinuationResponse ( Response response, string continuation )
    {
        response.Headers.AddHeader(KnownHeaders.ProxyAuthenticate, continuation);

        response.Headers.AddHeader(KnownHeaders.ProxyConnection, KnownHeaders.ConnectionKeepAlive);

        response.Headers.FixProxyHeaders();

        return response;
    }
}
