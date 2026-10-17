using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace CyberDucks.Shared.Telemetry;

/// <summary>
/// One-call OpenTelemetry setup for every range service: traces, metrics and logs over OTLP.
/// Service name, zone and collector endpoint come from the standard OTEL_* environment variables
/// (OTEL_SERVICE_NAME, OTEL_RESOURCE_ATTRIBUTES, OTEL_EXPORTER_OTLP_ENDPOINT), set in compose.yaml.
/// </summary>
public static class RangeTelemetry
{
    /// <summary>Activity source for the range's own spans (e.g. defence decisions).</summary>
    public static readonly ActivitySource Source = new("CyberDucks");

    public static IHostApplicationBuilder AddRangeTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var otel = builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(Source.Name)
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql")
                .AddMeter("CyberDucks.*"));  // the range's own meters (e.g. login attempts)

        // Only export when a collector is configured, so tests and local runs stay quiet.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }
}
