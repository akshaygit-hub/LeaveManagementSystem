using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Configuration;
using Steeltoe.Discovery.Client;
using System.Security.Claims;
using System.Text;

namespace Shared.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to configure common services across all microservices.
/// These extensions promote code reuse and ensure consistent configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures JWT Bearer authentication for the service.
    /// 
    /// Configuration:
    /// - Reads JwtSettings from appsettings.json (SecretKey, Issuer, Audience, ExpirationMinutes)
    /// - Validates issuer, audience, lifetime, and signature on incoming JWT tokens
    /// - Uses HS256 (HMAC-SHA256) symmetric key algorithm
    /// 
    /// Usage: Called in Program.cs of all services (ApiGateway, AuthService, LeaveService, NotificationService)
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Load JWT settings from appsettings.json, throw if missing
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not configured");

        // Register JwtSettings for dependency injection
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

        // Configure JWT Bearer authentication as default scheme
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Configure token validation parameters - all must pass for authentication to succeed
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                RoleClaimType = ClaimTypes.Role
            };
        });

        return services;
    }

    /// <summary>
    /// Configures Eureka service discovery client using Steeltoe.
    /// 
    /// Purpose:
    /// - Registers service with Eureka server for discovery by other services
    /// - Fetches registry of available service instances for client-side load balancing
    /// - Sends heartbeats to maintain registration
    /// 
    /// Configuration:
    /// - Reads Eureka settings from appsettings.json (ServiceUrl, AppName, etc.)
    /// - Used when UseEureka=true in configuration
    /// 
    /// Usage: Called conditionally in Program.cs when dynamic service discovery is enabled
    /// </summary>
    public static IServiceCollection AddEurekaServiceDiscovery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Steeltoe's AddDiscoveryClient reads Eureka configuration and sets up client
        services.AddDiscoveryClient(configuration);
        return services;
    }
}
