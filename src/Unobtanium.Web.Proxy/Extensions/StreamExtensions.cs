using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.StreamExtended.BufferPool;

namespace Unobtanium.Web.Proxy.Extensions;

/// <summary>
///     Extensions used for Stream and CustomBinaryReader objects
/// </summary>
internal static class StreamExtensions
{

    /// <summary>
    ///     Copy streams asynchronously
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="onCopy"></param>
    /// <param name="bufferPool"></param>
    /// <param name="cancellationToken"></param>
    internal static async Task CopyToAsync ( this Stream input, Stream output, Action<byte[], int, int>? onCopy,
        IBufferPool bufferPool, CancellationToken cancellationToken )
    {
        var buffer = bufferPool.GetBuffer();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // cancellation is not working on Socket ReadAsync
                // https://github.com/dotnet/corefx/issues/15033
                // https://github.com/dotnet/runtime/issues/23736 seems no longer needed
                var num = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                int bytesRead;
                if ((bytesRead = num) != 0 && !cancellationToken.IsCancellationRequested)
                {
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), CancellationToken.None);
                    onCopy?.Invoke(buffer, 0, bytesRead);
                }
                else
                {
                    break;
                }
            }
        }
        finally
        {
            bufferPool.ReturnBuffer(buffer);
        }
    }

    // NetworkStream.ReadAsync did not support cancellation token, seems fixed:
    // https://github.com/dotnet/runtime/issues/23736
    // This makes the below method redundant, but keeping it for now
    internal static async Task<T> WithCancellation<T> ( this Task<T> task, CancellationToken cancellationToken )
        where T : struct
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(s => (s as TaskCompletionSource<bool>)?.TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task)) return default;
        }

        return await task;
    }
}
