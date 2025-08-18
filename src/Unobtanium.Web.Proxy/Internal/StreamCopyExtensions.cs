using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Internal;

internal static class StreamCopyExtensions
{
    internal static async Task CopyDataAsync ( this Stream source, PipeWriter destination, string direction, ILogger logger, CancellationToken cancellationToken )
    {
        const int bufferSize = 8192;
        try
        {
            byte[] buffer = new byte[bufferSize];
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break; // End of stream

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                await destination.FlushAsync(cancellationToken);
                logger.LogDebug("Copied {BytesRead} bytes {Direction}", bytesRead, direction);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation, just exit
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error copying data {Direction}", direction);
        }
        finally
        {
            await destination.CompleteAsync();
        }
    }

    internal static async Task CopyDataAsync ( this PipeReader source, Stream destination, string direction, ILogger logger, CancellationToken cancellationToken )
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await source.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (buffer.IsEmpty && result.IsCompleted)
                {
                    break;
                }

                foreach (var segment in buffer)
                {
                    await destination.WriteAsync(segment, cancellationToken);
                }

                await destination.FlushAsync(cancellationToken);
                logger.LogDebug("Copied {BytesRead} bytes {Direction}", buffer.Length, direction);

                source.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation, just exit
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error copying data {Direction}", direction);
        }
        finally
        {
            await source.CompleteAsync();
        }
    }

}
