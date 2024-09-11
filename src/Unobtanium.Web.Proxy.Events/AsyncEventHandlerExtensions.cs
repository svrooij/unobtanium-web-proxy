using Microsoft.Extensions.Logging;

namespace Unobtanium.Web.Proxy.Events;
internal static class AsyncEventHandlerExtensions
{
    /// <summary>
    /// Invokes the event handler with a logger in a try-catch block.
    /// </summary>
    /// <typeparam name="T">Type of event arguments</typeparam>
    /// <param name="callback">The event</param>
    /// <param name="sender"></param>
    /// <param name="args">Event Arguments</param>
    /// <param name="logger">ILogger to use when this method throws an error.</param>
    /// <returns></returns>
    internal static async Task InvokeWithLoggerAsync<T> ( this AsyncEventHandler<T> callback, object sender, T args, CancellationToken cancellationToken,
        ILogger? logger )
    {
        var invocationList = callback.GetInvocationList();

        foreach (var @delegate in invocationList)
        {
            await InternalInvokeWithLoggerAsync((AsyncEventHandler<T>)@delegate, sender, args, cancellationToken, logger);
            if (cancellationToken.IsCancellationRequested)
                break;
        }
    }

    private static async Task InternalInvokeWithLoggerAsync<T> ( AsyncEventHandler<T> callback, object sender, T args,
        CancellationToken cancellationToken,
        ILogger? logger )
    {
        try
        {
            await callback(sender, args, cancellationToken);
        }
        catch (Exception e)
        {
            // Log the exception
            logger?.LogError(e, "An error occurred while invoking an event handler.");
        }
    }
}
