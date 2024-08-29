using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.StreamExtended.Network;

/// <summary>
/// Mimics a Task but allows setting of AsyncState.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TaskResult"/> class.
/// </remarks>
/// <param name="pTask">The task to be wrapped.</param>
/// <param name="state">The state object to be associated with the task.</param>
public class TaskResult ( Task pTask, object state ) : IAsyncResult
{

    /// <summary>
    /// Gets the state object associated with this task.
    /// </summary>
    public object AsyncState { get; } = state;

    /// <summary>
    /// Gets a WaitHandle that is used to wait for the task to complete.
    /// </summary>
    public WaitHandle AsyncWaitHandle => ((IAsyncResult)pTask).AsyncWaitHandle;

    /// <summary>
    /// Gets a value indicating whether the task completed synchronously.
    /// </summary>
    public bool CompletedSynchronously => ((IAsyncResult)pTask).CompletedSynchronously;

    /// <summary>
    /// Gets a value indicating whether the task has completed.
    /// </summary>
    public bool IsCompleted => pTask.IsCompleted;

    /// <summary>
    /// Blocks the calling thread until the task completes.
    /// </summary>
    public void GetResult ()
    {
        pTask.GetAwaiter().GetResult();
    }
}

/// <summary>
/// Mimics a Task&lt;T&gt; but allows setting of AsyncState.
/// </summary>
/// <typeparam name="T">The type of the result produced by the task.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="TaskResult{T}"/> class.
/// </remarks>
/// <param name="pTask">The task to be wrapped.</param>
/// <param name="state">The state object to be associated with the task.</param>
public class TaskResult<T> ( Task<T> pTask, object state ) : IAsyncResult
{

    /// <summary>
    /// Gets the result value of the task.
    /// </summary>
    public T Result => pTask.Result;

    /// <summary>
    /// Gets the state object associated with this task.
    /// </summary>
    public object AsyncState { get; } = state;

    /// <summary>
    /// Gets a WaitHandle that is used to wait for the task to complete.
    /// </summary>
    public WaitHandle AsyncWaitHandle => ((IAsyncResult)pTask).AsyncWaitHandle;

    /// <summary>
    /// Gets a value indicating whether the task completed synchronously.
    /// </summary>
    public bool CompletedSynchronously => ((IAsyncResult)pTask).CompletedSynchronously;

    /// <summary>
    /// Gets a value indicating whether the task has completed.
    /// </summary>
    public bool IsCompleted => pTask.IsCompleted;
}
