using Serilog;
using Shared.Extensions;
using Shared.Middleware;
using Shared.Configuration;
using LeaveService.Repositories;
using LeaveService.Services;
using LeaveService.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ========== LOGGING CONFIGURATION ==========
// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "LeaveService")
    .WriteTo.Console()
    .WriteTo.File("logs/leaveservice-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ========== SERVICE REGISTRATION ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========== DATA PERSISTENCE ==========
builder.Services.AddSingleton<ILeaveRepository, InMemoryLeaveRepository>();
Log.Information("LeaveService configured with InMemory data store");

// ========== RABBITMQ MESSAGING ==========
// Configure RabbitMQ for publishing leave events (LeaveApplied, LeaveApproved, LeaveRejected)
// NotificationService subscribes to these events for notification delivery
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

// ========== APPLICATION SERVICES ==========
builder.Services.AddScoped<ILeaveService, LeaveManagementService>();

// ========== AUTHENTICATION ==========
// Configure JWT Bearer authentication using shared extension method
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// ========== SERVICE DISCOVERY ==========
// Register with Eureka for service discovery
builder.Services.AddEurekaServiceDiscovery(builder.Configuration);

// ========== OPENTELEMETRY & JAEGER TRACING ==========
// Distributed tracing tracks leave operations across services
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
app.UseMiddleware<HealthCheckMiddleware>("LeaveService");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Starting Leave Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Leave Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
