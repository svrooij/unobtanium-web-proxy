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
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal static async Task InvokeWithLoggerAsync<T> ( this AsyncEventHandler<T> callback, object sender, T args, ILogger? logger, CancellationToken cancellationToken )
    {
        var invocationList = callback.GetInvocationList();

        foreach (var @delegate in invocationList)
        {
            await InternalInvokeWithLoggerAsync((AsyncEventHandler<T>)@delegate, sender, args, logger, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                break;
        }
    }

    private static async Task InternalInvokeWithLoggerAsync<T> ( AsyncEventHandler<T> callback, object sender, T args,
        ILogger? logger,
        CancellationToken cancellationToken
        )
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

    internal static async Task<TOutput?> InternalInvokeWithLoggerAsync<TArguments, TOutput> (
        AsyncEventHandler<TArguments, TOutput> callback,
        object sender,
        TArguments args,
        ILogger? logger,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await callback(sender, args, cancellationToken);
        }
        catch (Exception e)
        {
            // Log the exception
            logger?.LogError(e, "An error occurred while invoking an event handler.");
        }
        return default(TOutput); // Return default value if an exception occurs
    }
}
