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
        var application = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application == null)
        {
            return Result.Failure<ApplicationDto>($"Application with ID '{request.ApplicationId}' not found");
        }

        // Parse security tools if provided
        List<SecurityToolType>? tools = null;
        if (request.SecurityTools != null)
        {
            var toolResults = request.SecurityTools
                .Select(SecurityToolType.FromString)
                .ToList();

            var failedTool = toolResults.FirstOrDefault(r => r.IsFailure);
            if (failedTool != null)
            {
                return Result.Failure<ApplicationDto>(failedTool.Error);
            }

            tools = toolResults.Select(r => r.Value).ToList();
        }

        // Parse policy references if provided
        List<PolicyReference>? policies = null;
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

            policies = policyResults.Select(r => r.Value).ToList();
        }

        // Update environment configuration
        var result = application.UpdateEnvironmentConfig(
            request.EnvironmentName,
            tools,
            policies,
            request.IsActive);

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
