using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Commands;

/// <summary>
/// Handler for updating the owner of an application
/// </summary>
public class UpdateApplicationOwnerCommandHandler : IRequestHandler<UpdateApplicationOwnerCommand, Result<ApplicationDto>>
{
    private readonly IApplicationRepository _applicationRepository;

    public UpdateApplicationOwnerCommandHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<ApplicationDto>> Handle(UpdateApplicationOwnerCommand request, CancellationToken cancellationToken)
    {
        // Get application
        var applicationResult = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (applicationResult.IsFailure)
        {
            return Result.Failure<ApplicationDto>(applicationResult.Error);
        }

        var application = applicationResult.Value;

        // Update owner
        try
        {
            application.UpdateOwner(request.NewOwner);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ApplicationDto>(ex.Message);
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
