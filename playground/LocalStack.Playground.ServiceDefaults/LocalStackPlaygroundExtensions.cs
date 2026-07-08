#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using AWS.Messaging.Telemetry.OpenTelemetry;
using LocalStack.Playground.ServiceDefaults.ActivitySources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
/// </summary>
/// <remarks>
/// Reference this project from each service project in your solution.
/// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults.
/// </remarks>
public static class LocalStackPlaygroundExtensions
{
    /// <summary>
    /// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        return builder.AddServiceDefaults(static _ => { });
    }

    /// <summary>
    /// Adds common .NET Aspire services and lets the host add OpenTelemetry configuration without replacing the shared defaults.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configures optional service-default behavior for this host.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(
        this TBuilder builder,
        Action<LocalStackPlaygroundServiceDefaultsOptions> configure)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new LocalStackPlaygroundServiceDefaultsOptions();
        configure(options);

        ConfigureOpenTelemetry(builder, options);
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry using the shared playground defaults.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        return ConfigureOpenTelemetry(builder, new LocalStackPlaygroundServiceDefaultsOptions());
    }

    private static TBuilder ConfigureOpenTelemetry<TBuilder>(
        TBuilder builder,
        LocalStackPlaygroundServiceDefaultsOptions options)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                foreach (var configureTracing in options.TracingBeforeDefaults)
                {
                    configureTracing(tracing);
                }

                // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package).
                // tracing.AddGrpcClientInstrumentation();

                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAWSInstrumentation()
                    .AddAWSLambdaConfigurations(options => options.DisableAwsXRayContextExtraction = true)
                    .AddAWSMessagingInstrumentation()
                    .AddSource(UrlShortenerActivitySource.ActivitySourceName)
                    .AddSource(QrCodeGeneratorActivitySource.ActivitySourceName)
                    .AddSource(RedirectorActivitySource.ActivitySourceName);

                foreach (var configureTracing in options.TracingAfterDefaults)
                {
                    configureTracing(tracing);
                }
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static void AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Add a default liveness check to ensure the app is responsive.
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }
        // All health checks must pass for app to be considered ready to accept traffic after starting
        app.MapHealthChecks("/health");

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
        });

        return app;
    }
}

/// <summary>
/// Optional host-specific configuration for the Lambda playground service defaults.
/// </summary>
public sealed class LocalStackPlaygroundServiceDefaultsOptions
{
    private readonly List<Action<TracerProviderBuilder>> _tracingBeforeDefaults = [];
    private readonly List<Action<TracerProviderBuilder>> _tracingAfterDefaults = [];

    internal IReadOnlyList<Action<TracerProviderBuilder>> TracingBeforeDefaults => _tracingBeforeDefaults;

    internal IReadOnlyList<Action<TracerProviderBuilder>> TracingAfterDefaults => _tracingAfterDefaults;

    /// <summary>
    /// Adds tracing configuration that runs before the shared instrumentation defaults are registered.
    /// </summary>
    /// <param name="configure">The tracing configuration callback.</param>
    public void ConfigureTracingBeforeDefaults(Action<TracerProviderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _tracingBeforeDefaults.Add(configure);
    }

    /// <summary>
    /// Adds tracing configuration that runs after the shared instrumentation defaults are registered, but before exporters are added.
    /// </summary>
    /// <param name="configure">The tracing configuration callback.</param>
    public void ConfigureTracingAfterDefaults(Action<TracerProviderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _tracingAfterDefaults.Add(configure);
    }
}
