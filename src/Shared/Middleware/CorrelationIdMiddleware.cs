using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Diagnostics;

namespace Shared.Middleware;

/// <summary>
/// Middleware that ensures every request has a correlation ID for distributed tracing.
/// 
/// Purpose:
/// - Enables tracking of requests across multiple microservices
/// - If client provides X-Correlation-ID header, it's preserved and propagated
/// - If not provided, a new GUID is generated
/// - Correlation ID is added to response headers, Serilog log context, and OpenTelemetry spans
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get existing correlation ID from request header or generate new one
        var correlationId = GetOrGenerateCorrelationId(context);

        // Add correlation ID to response headers so client can track the request
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
            return Task.CompletedTask;
        });

        // ========== INTEGRATE WITH OPENTELEMETRY/JAEGER ==========
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("correlation.id", correlationId);
            activity.SetBaggage("correlation.id", correlationId);
        }

        // Push correlation ID to Serilog context - all logs in this request will include it
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private string GetOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            return correlationId.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
