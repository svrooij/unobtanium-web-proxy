using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Unobtanium.Web.Proxy.Examples.WorkerTemplate;

internal static class AspireLikeServiceExtensions
{
    internal static IHostApplicationBuilder AddSensibleDefault ( this IHostApplicationBuilder builder )
    {
        builder.ConfigureOpenTelemetry();

        //builder.AddDefaultHealthChecks();

        //builder.Services.AddServiceDiscovery();

        //builder.Services.ConfigureHttpClientDefaults(http =>
        //{
        //    // Turn on resilience by default
        //    http.AddStandardResilienceHandler();

        //    // Turn on service discovery by default
        //    //http.AddServiceDiscovery();
        //});

        return builder;
    }

    internal static IHostApplicationBuilder ConfigureOpenTelemetry ( this IHostApplicationBuilder builder )
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    //.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ProxyServerDefaults.ActivitySourceName)
                    //.AddAspNetCoreInstrumentation()
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters ( this IHostApplicationBuilder builder )
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }


}
