using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Commands;

/// <summary>
/// Handler for updating an application's risk tier
/// </summary>
public class UpdateApplicationRiskTierCommandHandler : IRequestHandler<UpdateApplicationRiskTierCommand, Result<ApplicationDto>>
{
    private readonly IApplicationRepository _applicationRepository;

    public UpdateApplicationRiskTierCommandHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<ApplicationDto>> Handle(UpdateApplicationRiskTierCommand request, CancellationToken cancellationToken)
    {
        // Get application
        var application = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application == null)
        {
            return Result.Failure<ApplicationDto>($"Application with ID '{request.ApplicationId}' not found");
        }

        // Create risk tier value object
        var riskTierResult = RiskTier.FromString(request.RiskTier);
        if (riskTierResult.IsFailure)
        {
            return Result.Failure<ApplicationDto>(riskTierResult.Error);
        }

        // Update risk tier
        var result = application.UpdateRiskTier(riskTierResult.Value);
        if (result.IsFailure)
        {
            return Result.Failure<ApplicationDto>(result.Error);
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
            RiskTier = application.RiskTier.Value,
            Owner = application.Owner,
            Environments = application.Environments.Select(e => new EnvironmentConfigDto
            {
                Id = e.Id,
                Name = e.Name,
                SecurityTools = e.SecurityTools.Select(t => t.Value).ToList(),
                PolicyReferences = e.PolicyReferences.Select(p => p.PackageName).ToList(),
                IsActive = e.IsActive
            }).ToList(),
            CreatedAt = application.CreatedAt,
            UpdatedAt = application.UpdatedAt
        };
    }
}
