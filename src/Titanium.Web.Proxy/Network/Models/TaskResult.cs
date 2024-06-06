using System;
using System.Threading;
using System.Threading.Tasks;

namespace Titanium.Web.Proxy.StreamExtended.Network;

/// <summary>
/// Mimics a Task but allows setting of AsyncState.
/// </summary>
public class TaskResult : IAsyncResult
{
    private readonly Task task;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskResult"/> class.
    /// </summary>
    /// <param name="pTask">The task to be wrapped.</param>
    /// <param name="state">The state object to be associated with the task.</param>
    public TaskResult(Task pTask, object state)
    {
        task = pTask;
        AsyncState = state;
    }

    /// <summary>
    /// Gets the state object associated with this task.
    /// </summary>
    public object AsyncState { get; }

    /// <summary>
    /// Gets a WaitHandle that is used to wait for the task to complete.
    /// </summary>
    public WaitHandle AsyncWaitHandle => ((IAsyncResult)task).AsyncWaitHandle;

    /// <summary>
    /// Gets a value indicating whether the task completed synchronously.
    /// </summary>
    public bool CompletedSynchronously => ((IAsyncResult)task).CompletedSynchronously;

    /// <summary>
    /// Gets a value indicating whether the task has completed.
    /// </summary>
    public bool IsCompleted => task.IsCompleted;

    /// <summary>
    /// Blocks the calling thread until the task completes.
    /// </summary>
    public void GetResult()
    {
        task.GetAwaiter().GetResult();
    }
}

/// <summary>
/// Mimics a Task&lt;T&gt; but allows setting of AsyncState.
/// </summary>
/// <typeparam name="T">The type of the result produced by the task.</typeparam>
public class TaskResult<T> : IAsyncResult
{
    private readonly Task<T> task;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskResult{T}"/> class.
    /// </summary>
    /// <param name="pTask">The task to be wrapped.</param>
    /// <param name="state">The state object to be associated with the task.</param>
    public TaskResult(Task<T> pTask, object state)
    {
        task = pTask;
        AsyncState = state;
    }

    /// <summary>
    /// Gets the result value of the task.
    /// </summary>
    public T Result => task.Result;

    /// <summary>
    /// Gets the state object associated with this task.
    /// </summary>
    public object AsyncState { get; }

    /// <summary>
    /// Gets a WaitHandle that is used to wait for the task to complete.
    /// </summary>
    public WaitHandle AsyncWaitHandle => ((IAsyncResult)task).AsyncWaitHandle;

    /// <summary>
    /// Gets a value indicating whether the task completed synchronously.
    /// </summary>
    public bool CompletedSynchronously => ((IAsyncResult)task).CompletedSynchronously;

    /// <summary>
    /// Gets a value indicating whether the task has completed.
    /// </summary>
    public bool IsCompleted => task.IsCompleted;
}