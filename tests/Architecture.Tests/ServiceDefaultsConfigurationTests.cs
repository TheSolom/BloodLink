using BloodLink.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using Shouldly;
using Xunit;

namespace BloodLink.Architecture.Tests;

public class ServiceDefaultsConfigurationTests
{
    [Fact]
    public void ConfigureOpenTelemetry_WhenStructuredEndpointConfigured_ShouldTakePrecedence()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:Endpoint"] = "http://structured-host:4317";
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://fallback-host:4317";

        builder.ConfigureOpenTelemetry();
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<OtlpExporterOptions>>().Value;
        options.Endpoint.ShouldBe(new Uri("http://structured-host:4317"));
    }

    [Fact]
    public void ConfigureOpenTelemetry_WhenStructuredEndpointIsWhitespace_ShouldFallbackToOtelEnvVar()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:Endpoint"] = "   ";
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://fallback-host:4317";

        builder.ConfigureOpenTelemetry();
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<OtlpExporterOptions>>().Value;
        options.Endpoint.ShouldBe(new Uri("http://fallback-host:4317"));
    }

    [Fact]
    public void ConfigureOpenTelemetry_WhenStandardDotnetEnvVarOverrideUsed_ShouldResolveCorrectly()
    {
        try
        {
            Environment.SetEnvironmentVariable("OpenTelemetry__Endpoint", "http://dotnet-env-host:4317");
            var builder = Host.CreateApplicationBuilder();

            builder.ConfigureOpenTelemetry();
            using var app = builder.Build();

            var options = app.Services.GetRequiredService<IOptions<OtlpExporterOptions>>().Value;
            options.Endpoint.ShouldBe(new Uri("http://dotnet-env-host:4317"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OpenTelemetry__Endpoint", null);
        }
    }

    [Fact]
    public void ConfigureOpenTelemetry_WhenBothEndpointsMissingOrWhitespace_ShouldNotThrowAndSkipRegistration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:Endpoint"] = "";
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "  ";

        Should.NotThrow(() => builder.ConfigureOpenTelemetry());
        using var app = builder.Build();
    }

    [Theory]
    [InlineData("not-a-valid-uri")]
    [InlineData("ftp://localhost:4317")]
    [InlineData("relative/path/endpoint")]
    public void ConfigureOpenTelemetry_WhenEndpointIsInvalid_ShouldThrowInvalidOperationException(string invalidEndpoint)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:Endpoint"] = invalidEndpoint;

        var ex = Should.Throw<InvalidOperationException>(() => builder.ConfigureOpenTelemetry());
        ex.Message.ShouldContain("invalid");
    }
}
