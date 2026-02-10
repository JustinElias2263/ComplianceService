using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Commands;

/// <summary>
/// Handler for updating an environment configuration
/// </summary>
public class UpdateEnvironmentConfigCommandHandler : IRequestHandler<UpdateEnvironmentConfigCommand, Result<ApplicationDto>>
{
    private readonly IApplicationRepository _applicationRepository;

    public UpdateEnvironmentConfigCommandHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<ApplicationDto>> Handle(UpdateEnvironmentConfigCommand request, CancellationToken cancellationToken)
    {
        // Get application
        var applicationResult = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (applicationResult.IsFailure)
        {
            return Result.Failure<ApplicationDto>(applicationResult.Error);
        }

        var application = applicationResult.Value;

        // Get environment
        var environmentResult = application.GetEnvironment(request.EnvironmentName);
        if (environmentResult.IsFailure)
        {
            return Result.Failure<ApplicationDto>(environmentResult.Error);
        }

        var environment = environmentResult.Value;

        // Update risk tier if provided
        if (!string.IsNullOrWhiteSpace(request.RiskTier))
        {
            var riskTierResult = RiskTier.Create(request.RiskTier);
            if (riskTierResult.IsFailure)
            {
                return Result.Failure<ApplicationDto>(riskTierResult.Error);
            }

            environment.UpdateRiskTier(riskTierResult.Value);
        }

        // Update security tools if provided
        if (request.SecurityTools != null)
        {
            var toolResults = request.SecurityTools
                .Select(SecurityToolType.Create)
                .ToList();

            var failedTool = toolResults.FirstOrDefault(r => r.IsFailure);
            if (failedTool != null)
            {
                return Result.Failure<ApplicationDto>(failedTool.Error);
            }

            var tools = toolResults.Select(r => r.Value).ToList();
            environment.UpdateSecurityTools(tools);
        }

        // Update policy references if provided
        if (request.PolicyReferences != null)
        {
            var policyResults = request.PolicyReferences
                .Select(PolicyReference.Create)
                .ToList();

            var failedPolicy = policyResults.FirstOrDefault(r => r.IsFailure);
            if (failedPolicy != null)
            {
                return Result.Failure<ApplicationDto>(failedPolicy.Error);
            }

            var policies = policyResults.Select(r => r.Value).ToList();
            environment.UpdatePolicies(policies);
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
            UpdatedAt = application.UpdatedAt
        };
    }
}
