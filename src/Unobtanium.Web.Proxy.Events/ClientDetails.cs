using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Events;
/// <summary>
/// Information about the client that is connecting to the proxy server.
/// </summary>
/// <param name="Address">Remote IP address of the client</param>
/// <param name="Port">Port the user is connecting from</param>
/// <param name="ConnectionTraceId"></param>
/// <param name="ConnectionSpanId"></param>
public record ClientDetails ( string Address, int Port, ActivityTraceId? ConnectionTraceId = null, ActivitySpanId? ConnectionSpanId = null )
{
    internal ActivityTraceId? ConnectionTraceId { get; } = ConnectionTraceId;
    internal ActivitySpanId? ConnectionSpanId { get; } = ConnectionSpanId;
    /// <summary>
    /// Returns a string representation of the client details.
    /// </summary>
    /// <returns>A string containing the address and port.</returns>
    public override string ToString ()
    {
        return $"{Address}:{Port}";
    }
}
