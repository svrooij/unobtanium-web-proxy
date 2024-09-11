using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Events;

namespace Unobtanium.Web.Proxy.Events;
/// <summary>
/// All events you can configure on the proxy server.
/// </summary>
public class ProxyServerEvents
{
    /// <summary>
    ///    Event to listen to new incoming requests.
    /// </summary>
    public event AsyncEventHandler<RequestEventArguments>? OnRequest;

    public bool HasOnRequest => OnRequest != null;

    public async Task InvokeOnRequest ( object sender, RequestEventArguments requestEventArguments, CancellationToken cancellationToken, ILogger? logger )
    {
        if (OnRequest != null)
            await OnRequest.InvokeWithLoggerAsync(sender, requestEventArguments, cancellationToken, logger);
    }
}
