using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceService.Application;

/// <summary>
/// Dependency injection configuration for the Application layer
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Application layer services with the DI container
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR for CQRS
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
        });

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
