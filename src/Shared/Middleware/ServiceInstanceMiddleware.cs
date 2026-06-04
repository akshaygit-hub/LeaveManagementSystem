using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Shared.Middleware;

/// <summary>
/// Middleware that adds service instance identification headers to responses.
/// Useful for verifying load balancing and debugging in distributed environments.
/// </summary>
public class ServiceInstanceMiddleware
{
    private readonly RequestDelegate _next;
    private const string ServiceInstanceHeader = "X-Service-Instance";
    private const string ServiceNameHeader = "X-Service-Name";
    private static readonly string HostName = Environment.MachineName;
    private static readonly string InstanceId = Guid.NewGuid().ToString("N")[..8];

    public ServiceInstanceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var serviceName = configuration.GetValue<string>("ServiceName") ?? "Unknown";

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(ServiceInstanceHeader, HostName);
            context.Response.Headers.TryAdd(ServiceNameHeader, serviceName);
            context.Response.Headers.TryAdd("X-Instance-Id", InstanceId);
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
