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
        catch (OperationCanceledException)
        {
            // Handle cancellation gracefully
            return default; // Return default value if the operation was cancelled
        }
        catch (Exception e)
        {
            // Log the exception
            logger?.LogError(e,
                             "Exception of type {ExceptionType} occurred in event handler for arguments of type {ArgumentsType}.",
                             e.GetType().Name,
                             typeof(TArguments).FullName);
        }
        return default; // Return default value if an exception occurs
    }
}
