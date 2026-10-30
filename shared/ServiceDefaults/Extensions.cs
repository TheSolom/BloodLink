using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace BloodLink.ServiceDefaults;

/// <summary>
/// Cross-cutting extensions providing OpenTelemetry instrumentation, health checks,
/// structured logging, and HTTP client resilience defaults across all BloodLink services.
/// Does not contain any business domain logic.
/// </summary>
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default (retries, circuit breaker, timeout)
            http.AddStandardResilienceHandler();
            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder
            .Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddMeter("MassTransit")
                    .AddMeter("BloodLink.Inventory")
                    .AddMeter("BloodLink.Requests")
                    .AddMeter("BloodLink.Dispatch");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("MassTransit")
                    .AddSource("Microsoft.EntityFrameworkCore")
                    .AddSource("BloodLink.*");
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        string? structuredEndpoint = builder.Configuration["OpenTelemetry:Endpoint"];
        string? fallbackEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        string? endpointStr = !string.IsNullOrWhiteSpace(structuredEndpoint)
            ? structuredEndpoint.Trim()
            : (!string.IsNullOrWhiteSpace(fallbackEndpoint) ? fallbackEndpoint.Trim() : null);

        if (endpointStr is not null)
        {
            if (!Uri.TryCreate(endpointStr, UriKind.Absolute, out Uri? endpointUri) ||
                (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"The configured OpenTelemetry endpoint '{endpointStr}' is invalid. It must be an absolute HTTP or HTTPS URL.");
            }

            builder.Services.Configure<OtlpExporterOptions>(options =>
            {
                options.Endpoint = endpointUri;
            });

            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder
            .Services.AddHealthChecks()
            // Add basic liveness check tagged with 'live'
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Liveness probe (/health/live) - checks only if the process is up and running
        app.MapHealthChecks(
            "/health/live",
            new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") }
        );

        // Readiness probe (/health/ready) - checks critical dependencies (SQL Server, RabbitMQ)
        app.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }
        );

        return app;
    }
}
