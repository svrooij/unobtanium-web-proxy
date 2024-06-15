using System;

namespace Titanium.Web.Proxy.Exceptions;
/// <summary>
/// Exception thrown in user event, this is a wrapper exception for all exceptions thrown in user event handlers
/// </summary>
public class EventException : Exception
{
    internal EventException (Exception? innerException): base("Exception thrown in user event", innerException)
    {
    }
}
