using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace ApiGateway.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry distributed tracing with Jaeger.
/// Tracks gateway requests and downstream service calls.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing with Jaeger exporter for API Gateway.
    /// Instruments: ASP.NET Core (incoming requests), HttpClient (downstream calls)
    /// </summary>
    public static IServiceCollection AddOpenTelemetryTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("api-gateway"))
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
                    })
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = configuration["JAEGER_AGENT_HOST"] ?? "localhost";
                        options.AgentPort = int.Parse(configuration["JAEGER_AGENT_PORT"] ?? "6831");
                        Log.Information("Jaeger tracing: {Host}:{Port}", options.AgentHost, options.AgentPort);
                    });
            });

        Log.Information("OpenTelemetry distributed tracing configured for ApiGateway");
        return services;
    }
}
