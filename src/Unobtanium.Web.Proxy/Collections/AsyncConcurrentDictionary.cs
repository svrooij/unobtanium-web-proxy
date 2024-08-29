using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Collections;

/// <summary>
/// A concurrent dictionary that allows async <see cref="GetOrAddAsync"/> operation
/// while locking on the key when executing the factory method
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
/// <remarks>
/// This is useful when you want to avoid multiple factory methods for the same key
/// </remarks>
internal class AsyncConcurrentDictionary<TKey, TValue> : ConcurrentDictionary<TKey, TValue> where TKey : notnull
{
    internal AsyncConcurrentDictionary () : base() { }

    /// <summary>
    /// Get a value for a key or await the factory method to create it, while locking on the key when executing the factory method
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="factory">Factory method to create the value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Value</returns>
    public async Task<TValue> GetOrAddAsync ( TKey key, Func<CancellationToken, Task<TValue>> factory, CancellationToken cancellationToken )
    {
        // First attempt to get the value without locking
        if (base.TryGetValue(key, out TValue? value))
        {
            return value;
        }

        // Acquire the semaphore for the key to ensure single execution of the factory
        var semaphoreSlim = GetSemaphoreSlim(key);
        await semaphoreSlim.WaitAsync(cancellationToken);

        try
        {
            // Double-check if the key was added while waiting for the semaphore
            if (base.TryGetValue(key, out value))
            {
                return value;
            }

            // Execute the factory method to create the value
            value = await factory(cancellationToken);
            base.TryAdd(key, value);
        }
        finally
        {
            semaphoreSlim.Release();
        }

        return value;
    }

    /// <summary>
    /// Get or create a semaphore for a key
    /// </summary>
    /// <param name="key"></param>
    private SemaphoreSlim GetSemaphoreSlim ( TKey key ) => _dicSemaphoreSlim.GetOrAdd(key, x => new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(1, 1))).Value;

    /// <summary>
    /// Dictionary to store semaphores for each key
    /// </summary>
    private readonly ConcurrentDictionary<TKey, Lazy<SemaphoreSlim>> _dicSemaphoreSlim = new ConcurrentDictionary<TKey, Lazy<SemaphoreSlim>>();

    ~AsyncConcurrentDictionary ()
    {
        foreach (var semaphore in _dicSemaphoreSlim)
        {
            semaphore.Value.Value.Dispose();
        }
    }
}
