using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.EventArguments;
using Unobtanium.Web.Proxy.Exceptions;

namespace Unobtanium.Web.Proxy.Extensions;

internal static class FuncExtensions
{
    internal static async Task InvokeAsync<T> ( this AsyncEventHandler<T> callback, object sender, T args,
        ILogger? logger)
    {
        var invocationList = callback.GetInvocationList();

        foreach (var @delegate in invocationList)
            await InternalInvokeAsync((AsyncEventHandler<T>)@delegate, sender, args, logger);
    }

    private static async Task InternalInvokeAsync<T> ( AsyncEventHandler<T> callback, object sender, T args,
        ILogger? logger )
    {
        try
        {
            await callback(sender, args);
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Error whilst invoking callback {CallbackName}", callback.Method.Name);
        }
    }
}
