using Serilog;
using Shared.Extensions;
using Shared.Middleware;
using AuthService.Repositories;
using AuthService.Services;
using AuthService.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ========== LOGGING CONFIGURATION ==========
// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "AuthService")
    .WriteTo.Console()
    .WriteTo.File("logs/authservice-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ========== SERVICE REGISTRATION ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========== DATA PERSISTENCE ==========
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
Log.Information("AuthService configured with InMemory data store");

// ========== APPLICATION SERVICES ==========
builder.Services.AddScoped<IAuthService, AuthenticationService>();

// ========== AUTHENTICATION ==========
// Configure JWT Bearer authentication using shared extension method
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// ========== SERVICE DISCOVERY ==========
// Register with Eureka for service discovery
// Other services can find AuthService by name instead of hardcoded URLs
builder.Services.AddEurekaServiceDiscovery(builder.Configuration);

// ========== OPENTELEMETRY & JAEGER TRACING ==========
// Distributed tracing tracks authentication requests across services
builder.Services.AddOpenTelemetryTracing(builder.Configuration);

// ========== CORS CONFIGURATION ==========
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ========== HTTP PIPELINE CONFIGURATION ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ServiceInstanceMiddleware>();
app.UseMiddleware<HealthCheckMiddleware>("AuthService");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Starting Auth Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Auth Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
