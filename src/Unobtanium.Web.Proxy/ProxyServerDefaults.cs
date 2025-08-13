using System.Diagnostics;

namespace Unobtanium.Web.Proxy;

public static class ProxyServerDefaults
{
    public const string ACTIVITY_SOURCE_NAME = "Unobtanium.Web.Proxy";
    internal static readonly ActivitySource ProxyActivitySource = new ActivitySource(ACTIVITY_SOURCE_NAME);

    public const int DEFAULT_PORT = 8000;
    public const int DEFAULT_HTTPS_PORT = 8001;
}
