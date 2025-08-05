using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Unobtanium.Web.Proxy.Extensions;

/// <summary>
/// Extension methods for Activity to support additional telemetry operations
/// </summary>
internal static class ActivityExtensions
{
    /// <summary>
    /// Records an exception in the activity
    /// </summary>
    /// <param name="activity">The activity to record the exception in</param>
    /// <param name="exception">The exception to record</param>
    /// <param name="escaped">Whether the exception escaped the activity scope</param>
    public static void RecordException(this Activity? activity, Exception exception, bool escaped = true)
    {
        if (activity == null) return;

        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stacktrace", exception.ToString());
        activity.SetTag("exception.escaped", escaped.ToString().ToLowerInvariant());
        
        if (escaped)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }
    }
}
