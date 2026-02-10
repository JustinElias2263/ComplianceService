using ComplianceService.Application.Interfaces;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.Evaluation.Interfaces;
using ComplianceService.Infrastructure.ExternalServices;
using ComplianceService.Infrastructure.Persistence;
using ComplianceService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceService.Infrastructure;

/// <summary>
/// Dependency injection configuration for Infrastructure layer
/// Registers database context, repositories, and external services
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Infrastructure layer services to the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database Context
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("ComplianceDatabase")
                ?? throw new InvalidOperationException("Database connection string 'ComplianceDatabase' not found");

            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                })
                .EnableSensitiveDataLogging(configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging"))
                .EnableDetailedErrors(configuration.GetValue<bool>("Logging:EnableDetailedErrors"));
        });

        // Repositories
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IComplianceEvaluationRepository, ComplianceEvaluationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // External Services - OPA HTTP Client
        services.AddHttpClient<IOpaClient, OpaHttpClient>(client =>
        {
            var opaBaseUrl = configuration["OpaSettings:BaseUrl"] ?? "http://localhost:8181";
            client.BaseAddress = new Uri(opaBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue<int>("OpaSettings:TimeoutSeconds", 30));
        });

        // Notification Service
        services.AddScoped<INotificationService, LoggingNotificationService>();

        return services;
    }

    /// <summary>
    /// Applies pending database migrations automatically on startup
    /// WARNING: Only use in development/staging. In production, use explicit migration scripts.
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    public static void ApplyMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (dbContext.Database.GetPendingMigrations().Any())
        {
            dbContext.Database.Migrate();
        }
    }
}
