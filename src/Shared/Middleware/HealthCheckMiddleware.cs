using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Shared.Middleware;

/// <summary>
/// Lightweight health check middleware that responds to /health endpoint.
/// 
/// Purpose:
/// - Provides quick health status endpoint for monitoring tools (Docker, Kubernetes, load balancers)
/// - Returns service name, status, and timestamp
/// - Does not check database connections or external dependencies (fast response)
/// </summary>
public class HealthCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _serviceName;
    private readonly string? _additionalInfo;

    public HealthCheckMiddleware(RequestDelegate next, IConfiguration configuration, string serviceName, string? additionalInfo = null)
    {
        _next = next;
        _serviceName = serviceName;
        _additionalInfo = additionalInfo;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = "Healthy",
                service = _serviceName,
                timestamp = DateTime.UtcNow,
                additionalInfo = _additionalInfo
            };

            await context.Response.WriteAsJsonAsync(response);
            return;
        }

        await _next(context);
    }
}
