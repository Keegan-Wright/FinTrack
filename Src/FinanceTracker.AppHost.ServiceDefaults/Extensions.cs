using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FinanceTracker.AppHost.ServiceDefaults;

// Adds common Aspire services service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    internal const string HealthEndpointPath = "/health";

    /// <summary>
    /// The activity source and meter Quartz 4 publishes through, <c>Quartz.Diagnostics.QuartzInstrumentation</c>'s
    /// ActivitySourceName and MeterName.
    /// </summary>
    private const string QuartzInstrumentationName = "Quartz";
    internal const string AlivenessEndpointPath = "/alive";

    /// <summary>
    /// The meter EF Core publishes through.
    /// </summary>
    private const string EntityFrameworkCoreMeterName = "Microsoft.EntityFrameworkCore";

    /// <summary>
    /// The activity source ASP.NET Core's SignalR server publishes hub method calls through.
    /// </summary>
    private const string SignalRServerActivitySource = "Microsoft.AspNetCore.SignalR.Server";

    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        public void AddServiceDefaults()
        {
            builder.ConfigureOpenTelemetry();

            builder.AddDefaultHealthChecks();

            builder.Services.AddServiceDiscovery();

            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                // Turn on resilience by default
                http.AddStandardResilienceHandler(o =>
                {
                    o.Retry.UseJitter = true;
                });

                // Turn on service discovery by default
                http.AddServiceDiscovery();
            });
        }

        private void ConfigureOpenTelemetry()
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });


            // The service's name and instance come from OTEL_SERVICE_NAME and OTEL_RESOURCE_ATTRIBUTES, which Aspire
            // sets so the dashboard files the telemetry under the app's resource. Naming it here would override them
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(x =>
                    x.AddProcessDetector()
                        .AddProcessRuntimeDetector()
                        .AddOperatingSystemDetector()
                        .AddHostDetector()
                        .AddContainerDetector())
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();

                    metrics.AddNpgsqlInstrumentation();

                    // EF Core's own active contexts, queries and SaveChanges calls, compiled query cache hits and
                    // misses, optimistic concurrency failures and execution strategy retries
                    metrics.AddMeter(EntityFrameworkCoreMeterName);

                    // Job run times, retries, misfires and the store's operations
                    metrics.AddMeter(QuartzInstrumentationName);
                })
                .WithTracing(tracing =>
                {
                    // A span for each job run and each store write, the jobs' own spans inside them
                    tracing.AddSource(QuartzInstrumentationName);

                    // Each hub method call the browser makes, ASP.NET Core's own source
                    tracing.AddSource(SignalRServerActivitySource);

                    // The app's own spans, the services' under its name too, e.g. FinanceTracker.Infrastructure
                    tracing.AddSource($"{builder.Environment.ApplicationName}.*");

                    tracing.AddSource(builder.Environment.ApplicationName)
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.Filter = TraceFilters.IsTraced;

                            // An exception the request threw, on its span as well as in the logs
                            options.RecordException = true;
                        })
                        .AddHttpClientInstrumentation(options => options.RecordException = true)
                        .AddNpgsql();
                });
            builder.AddOpenTelemetryExporters();
        }

        private void AddOpenTelemetryExporters()
        {
            bool useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            if (useOtlpExporter)
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }
        }

        private void AddDefaultHealthChecks()
        {
            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        }
    }

    extension(WebApplication app)
    {
        public WebApplication MapDefaultEndpoints()
        {
            if (!app.Environment.IsDevelopment())
            {
                return app;
            }

            app.MapHealthChecks(HealthEndpointPath);

            app.MapHealthChecks(AlivenessEndpointPath,
                new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });

            return app;
        }
    }
}
