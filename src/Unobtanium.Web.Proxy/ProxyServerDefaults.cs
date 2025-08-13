using System.Diagnostics;

namespace Unobtanium.Web.Proxy;

/// <summary>
/// Provides default values and constants for configuring a proxy server.
/// </summary>
/// <remarks>This class contains predefined constants such as default port numbers and activity source names that
/// can be used when setting up or interacting with a proxy server. These values are intended to simplify configuration
/// and ensure consistency across implementations.</remarks>
public static class ProxyServerDefaults
{
    /// <summary>
    /// Represents the name of the activity source used for tracing in the Unobtanium Web Proxy.
    /// </summary>
    /// <remarks>This constant is used to identify the activity source when emitting telemetry data. It can be
    /// utilized in distributed tracing scenarios to correlate activities across services.</remarks>
    public const string ACTIVITY_SOURCE_NAME = "Unobtanium.Web.Proxy";
    internal static readonly ActivitySource ProxyActivitySource = new ActivitySource(ACTIVITY_SOURCE_NAME);

    /// <summary>
    /// The default port number used for the proxy server.
    /// </summary>
    public const int DEFAULT_PORT = 8000;

    /// <summary>
    /// The default port number used for HTTPS connections.
    /// </summary>
    public const int DEFAULT_HTTPS_PORT = 8001;
}
