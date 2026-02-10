using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using ComplianceService.Domain.Shared;
using MediatR;
using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;

namespace ComplianceService.Application.Handlers.Commands;

/// <summary>
/// Handler for registering a new application
/// </summary>
public class RegisterApplicationCommandHandler : IRequestHandler<RegisterApplicationCommand, Result<ApplicationDto>>
{
    private readonly IApplicationRepository _applicationRepository;

    public RegisterApplicationCommandHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<ApplicationDto>> Handle(RegisterApplicationCommand request, CancellationToken cancellationToken)
    {
        // Check if application already exists
        var existingResult = await _applicationRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existingResult.IsSuccess)
        {
            return Result.Failure<ApplicationDto>($"Application with name '{request.Name}' already exists");
        }

        // Create application aggregate
        var applicationResult = DomainApplication.Create(request.Name, request.Owner);
        if (applicationResult.IsFailure)
        {
            return Result.Failure<ApplicationDto>(applicationResult.Error);
        }

        // Persist
        var application = applicationResult.Value;
        await _applicationRepository.AddAsync(application, cancellationToken);
        await _applicationRepository.SaveChangesAsync(cancellationToken);

        // Map to DTO
        return Result.Success(MapToDto(application));
    }

    private static ApplicationDto MapToDto(DomainApplication application)
    {
        return new ApplicationDto
        {
            Id = application.Id,
            Name = application.Name,
            Owner = application.Owner,
            Environments = application.Environments.Select(e => new EnvironmentConfigDto
            {
                Id = e.Id,
                Name = e.EnvironmentName,
                RiskTier = e.RiskTier.Value,
                SecurityTools = e.SecurityTools.Select(t => t.Name).ToList(),
                PolicyReferences = e.Policies.Select(p => p.PackageName).ToList(),
                IsActive = application.IsActive
            }).ToList(),
            CreatedAt = application.CreatedAt,
            UpdatedAt = application.UpdatedAt.GetValueOrDefault(application.CreatedAt)
        };
    }
}
