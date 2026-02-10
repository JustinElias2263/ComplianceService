using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting all applications with filtering and pagination
/// </summary>
public class GetAllApplicationsQueryHandler : IRequestHandler<GetAllApplicationsQuery, Result<IReadOnlyList<ApplicationDto>>>
{
    private readonly IApplicationRepository _applicationRepository;

    public GetAllApplicationsQueryHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<IReadOnlyList<ApplicationDto>>> Handle(GetAllApplicationsQuery request, CancellationToken cancellationToken)
    {
        var applications = await _applicationRepository.GetAllAsync(cancellationToken);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.Owner))
        {
            applications = applications.Where(a =>
                a.Owner.Equals(request.Owner, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Note: RiskTier filter removed as risk tier is now per-environment, not per-application

        // Apply pagination
        var skip = (request.PageNumber - 1) * request.PageSize;
        var pagedApplications = applications
            .Skip(skip)
            .Take(request.PageSize)
            .ToList();

        var dtos = pagedApplications.Select(application => new ApplicationDto
        {
            Id = application.Id,
            Name = application.Name,
            Owner = application.Owner,
            Environments = application.Environments.Select(e => new EnvironmentConfigDto
            {
                Id = e.Id,
                Name = e.Name,
                RiskTier = e.RiskTier.Value,
                SecurityTools = e.SecurityTools.Select(t => t.Value).ToList(),
                PolicyReferences = e.PolicyReferences.Select(p => p.PackageName).ToList(),
                IsActive = e.IsActive
            }).ToList(),
            CreatedAt = application.CreatedAt,
            UpdatedAt = application.UpdatedAt
        }).ToList();

        return Result.Success<IReadOnlyList<ApplicationDto>>(dtos);
    }
}
