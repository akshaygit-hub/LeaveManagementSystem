using Serilog;
using Shared.Extensions;
using Shared.Middleware;
using Shared.Configuration;
using NotificationService.Repositories;
using NotificationService.Services;
using NotificationService.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ========== LOGGING CONFIGURATION ==========
// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "NotificationService")
    .WriteTo.Console()
    .WriteTo.File("logs/notificationservice-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ========== SERVICE REGISTRATION ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========== DATA PERSISTENCE ==========
builder.Services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
Log.Information("NotificationService configured with InMemory data store");

// ========== RABBITMQ MESSAGING ==========
// Configure RabbitMQ consumer for processing leave events
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddHostedService<RabbitMQConsumerService>();

// ========== AUTHENTICATION ==========
// Configure JWT Bearer authentication using shared extension method
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// ========== SERVICE DISCOVERY ==========
// Register with Eureka for service discovery
builder.Services.AddEurekaServiceDiscovery(builder.Configuration);

// ========== OPENTELEMETRY & JAEGER TRACING ==========
// Distributed tracing for tracking notification processing from RabbitMQ messages
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
app.UseMiddleware<HealthCheckMiddleware>("NotificationService");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Starting Notification Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Notification Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
