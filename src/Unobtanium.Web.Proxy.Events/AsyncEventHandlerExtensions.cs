using Microsoft.Extensions.Logging;

namespace Unobtanium.Web.Proxy.Events;
internal static class AsyncEventHandlerExtensions
{
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
