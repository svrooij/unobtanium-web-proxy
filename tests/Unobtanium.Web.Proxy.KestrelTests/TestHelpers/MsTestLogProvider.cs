using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.KestrelTests.TestHelpers;

using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[UnsupportedOSPlatform("browser")]
[ProviderAlias("MsTest")]
internal sealed class MsTestLoggerProvider : ILoggerProvider
{
    private readonly TestContext _testContext;
    private ILogger? _logger;

    public MsTestLoggerProvider (
        TestContext testContext)
    {
        _testContext = testContext ?? throw new ArgumentNullException(nameof(testContext));
    }

    public ILogger CreateLogger ( string categoryName ) => 
        _logger ??= new MsTestLogger(_testContext);


    public void Dispose ()
    {
       
    }
}
