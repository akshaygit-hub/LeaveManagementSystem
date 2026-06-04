using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Eureka;
using Serilog;
using Shared.Extensions;
using Shared.Middleware;
using ApiGateway.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ========== OCELOT CONFIGURATION SELECTION ==========
// Dynamically choose between static routing (hardcoded URLs) and Eureka-based service discovery
// UseEureka flag is read from appsettings.json
var useEureka = builder.Configuration.GetValue<bool>("UseEureka", false);
var ocelotConfigFile = useEureka ? "ocelot.eureka.json" : "ocelot.static.json";

// Load the selected Ocelot configuration file
// - ocelot.static.json: Contains DownstreamHostAndPorts with hardcoded service URLs
// - ocelot.eureka.json: Contains ServiceName for dynamic discovery via Eureka
builder.Configuration.AddJsonFile(ocelotConfigFile, optional: false, reloadOnChange: true);

// ========== LOGGING CONFIGURATION ==========
// Configure Serilog for structured logging with multiple sinks
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ApiGateway")
    .WriteTo.Console()
    .WriteTo.File("logs/apigateway-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ========== JWT AUTHENTICATION ==========
// Add JWT Bearer authentication using shared configuration extension
builder.Services.AddJwtAuthentication(builder.Configuration);

// ========== SERVICE DISCOVERY ==========
// Conditionally add Eureka service discovery based on UseEureka flag
// When enabled: Gateway can discover and communicate with services registered in Eureka
if (useEureka)
{
    builder.Services.AddEurekaServiceDiscovery(builder.Configuration);
    Log.Information("API Gateway configured with Eureka service discovery");
}
else
{
    Log.Information("API Gateway configured with static routing");
}

// ========== OCELOT API GATEWAY SETUP ==========
// Configure Ocelot with routing and request forwarding
var ocelotBuilder = builder.Services.AddOcelot(builder.Configuration);

// Add Eureka provider to Ocelot if service discovery is enabled
// This allows Ocelot to resolve ServiceName from ocelot.eureka.json to actual service URLs
if (useEureka)
{
    ocelotBuilder.AddEureka();
    Log.Information("Ocelot configured with Eureka provider for dynamic ServiceName-based discovery");
}
else
{
    Log.Information("Ocelot configured with static DownstreamHostAndPorts");
}

// ========== CORS CONFIGURATION ==========
// Allow all origins, methods, and headers for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ========== OPENTELEMETRY & JAEGER TRACING ==========
// Distributed tracing allows tracking requests as they flow through multiple services
builder.Services.AddOpenTelemetryTracing(builder.Configuration);

var app = builder.Build();

// ========== MIDDLEWARE PIPELINE ==========
// ORDER IS CRITICAL! Middleware executes in the order added
app.UseCors();

// Add correlation ID for distributed tracing across services
app.UseMiddleware<CorrelationIdMiddleware>();

// Add service instance identifier to response headers (for debugging load balancing)
app.UseMiddleware<ServiceInstanceMiddleware>();

// Intercept /health endpoint and return health status
app.UseMiddleware<HealthCheckMiddleware>("ApiGateway", useEureka ? "Eureka" : "Static");

// Validate JWT tokens and populate User claims
app.UseAuthentication();

// Check authorization rules
app.UseAuthorization();

try
{
    Log.Information("Starting API Gateway with {Mode} routing", useEureka ? "Eureka" : "Static");

    // ========== OCELOT MIDDLEWARE ==========
    // MUST BE LAST! Routes all requests through Ocelot
    await app.UseOcelot();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
