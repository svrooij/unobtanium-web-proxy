using System;
using System.Net;
using Titanium.Web.Proxy.StreamExtended;

namespace Titanium.Web.Proxy.Http;

/// <summary>
/// The tcp tunnel connect response object.
/// </summary>
public class ConnectResponse : Response
{
    /// <summary>
    /// Gets or sets the server hello information.
    /// </summary>
    public ServerHelloInfo? ServerHelloInfo { get; set; }

    /// <summary>
    /// Creates a successful CONNECT response.
    /// </summary>
    /// <param name="httpVersion">The HTTP version.</param>
    /// <returns>The <see cref="ConnectResponse"/>.</returns>
    internal static ConnectResponse CreateSuccessfulConnectResponse ( Version httpVersion )
    {
        var response = new ConnectResponse
        {
            HttpVersion = httpVersion,
            StatusCode = (int)HttpStatusCode.OK,
            StatusDescription = "Connection Established"
        };

        return response;
    }
}
