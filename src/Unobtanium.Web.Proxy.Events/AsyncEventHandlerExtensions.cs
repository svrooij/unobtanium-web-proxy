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
            if (args is ResponseEventArguments respArgs && logger != null)
            {
                logger.LogError(e, "An error occurred while processing request: {RequestId} to {RequestUri} {ErrorMessage}", respArgs.RequestId, respArgs.Request.RequestUri, e.Message);
            }
            else if (args is RequestEventArguments reqArgs && logger != null)
            {
                logger.LogError(e, "An error occurred while processing request: {RequestId} to {RequestUri} {ErrorMessage}", reqArgs.RequestId, reqArgs.Request.RequestUri, e.Message);
            }
            else if (logger != null)
            {
                logger.LogError(e, "An error occurred during event handling.");
            }
        }
        return default; // Return default value if an exception occurs
    }
}
