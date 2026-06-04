using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace AuthService.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry distributed tracing with Jaeger.
/// Tracks HTTP requests and authentication operations.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing with Jaeger exporter for AuthService.
    /// Instruments: ASP.NET Core (incoming requests), HttpClient (outgoing calls)
    /// </summary>
    public static IServiceCollection AddOpenTelemetryTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("auth-service"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;

                        // Filter out Eureka service discovery calls from traces
                        options.FilterHttpRequestMessage = (httpRequestMessage) =>
                        {
                            return !httpRequestMessage.RequestUri?.ToString().Contains("/eureka/") ?? true;
                        };
                    })
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = configuration["JAEGER_AGENT_HOST"] ?? "localhost";
                        options.AgentPort = int.Parse(configuration["JAEGER_AGENT_PORT"] ?? "6831");
                        Log.Information("Jaeger tracing: {Host}:{Port}", options.AgentHost, options.AgentPort);
                    });
            });

        Log.Information("OpenTelemetry distributed tracing configured for AuthService");
        return services;
    }
}
