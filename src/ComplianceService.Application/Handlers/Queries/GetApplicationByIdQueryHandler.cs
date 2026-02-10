using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting an application by ID
/// </summary>
public class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, Result<ApplicationDto>>
{
    private readonly IApplicationRepository _applicationRepository;

    public GetApplicationByIdQueryHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<ApplicationDto>> Handle(GetApplicationByIdQuery request, CancellationToken cancellationToken)
    {
        var applicationResult = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);

        if (applicationResult.IsFailure)
        {
            return Result.Failure<ApplicationDto>(applicationResult.Error);
        }

        var application = applicationResult.Value;

        return Result.Success(new ApplicationDto
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
        });
    }
}
