using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace IoBuild.Api.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddIoBuildObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        // Guard: app must start healthy when jaeger absent
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return services;
        }

        // Normalize endpoint — ensure Uri parsing succeeds
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var otlpUri))
        {
            return services;
        }

        try
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("iobuild-api"))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;
                        })
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = otlpUri;
                        });
                });
        }
        catch
        {
            // Never fail startup due to observability misconfiguration
        }

        return services;
    }
}
