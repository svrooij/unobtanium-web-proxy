using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.KestrelTests.TestHelpers;
/// <summary>
/// MsTestLogger for ILogger
/// </summary>
public class MsTestLogger : ILogger, IDisposable
{
    private TestContext _output;

    public MsTestLogger ( TestContext output )
    {
        _output = output;
    }
    public void Log<TState> ( LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter )
    {
        _output.WriteLine("{0}: {1}\n{2}", logLevel, state, (exception == null) ? string.Empty : exception.ToString());
    }

    public bool IsEnabled ( LogLevel logLevel )
    {
        return true;
    }

    public IDisposable BeginScope<TState> ( TState state )
    {
        return this;
    }

    public void Dispose ()
    {
    }
}
