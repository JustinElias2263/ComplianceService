using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Commands;

/// <summary>
/// Handler for deactivating (removing) an environment configuration
/// </summary>
public class DeactivateEnvironmentCommandHandler : IRequestHandler<DeactivateEnvironmentCommand, Result<ApplicationDto>>
{
    private readonly IApplicationRepository _applicationRepository;

    public DeactivateEnvironmentCommandHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<ApplicationDto>> Handle(DeactivateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        // Get application
        var applicationResult = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (applicationResult.IsFailure)
        {
            return Result.Failure<ApplicationDto>(applicationResult.Error);
        }

        var application = applicationResult.Value;

        // Remove environment
        var removeResult = application.RemoveEnvironment(request.EnvironmentName);
        if (removeResult.IsFailure)
        {
            return Result.Failure<ApplicationDto>(removeResult.Error);
        }

        // Persist
        await _applicationRepository.UpdateAsync(application, cancellationToken);
        await _applicationRepository.SaveChangesAsync(cancellationToken);

        // Map to DTO
        return Result.Success(MapToDto(application));
    }

    private static ApplicationDto MapToDto(Domain.ApplicationProfile.Application application)
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
