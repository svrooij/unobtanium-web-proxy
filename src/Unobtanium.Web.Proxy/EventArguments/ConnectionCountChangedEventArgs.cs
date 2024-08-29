using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.EventArguments;
/// <summary>
/// Event arguments for connection count change event.
/// </summary>
/// <param name="NewValue">New connection count.</param>
/// <param name="OldValue">Old connection count.</param>
public class ConnectionCountChangedEventArgs ( int OldValue, int NewValue ) : EventArgs
{
    /// <summary>
    /// Old connection count.
    /// </summary>
    public int OldValue { get; } = OldValue;

    /// <summary>
    /// New connection count.
    /// </summary>
    public int NewValue { get; } = NewValue;
}
