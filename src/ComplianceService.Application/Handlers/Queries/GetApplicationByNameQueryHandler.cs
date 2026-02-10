using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting an application by name
/// </summary>
public class GetApplicationByNameQueryHandler : IRequestHandler<GetApplicationByNameQuery, Result<ApplicationDto>>
{
    private readonly IApplicationRepository _applicationRepository;

    public GetApplicationByNameQueryHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<ApplicationDto>> Handle(GetApplicationByNameQuery request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository.GetByNameAsync(request.Name, cancellationToken);

        if (application == null)
        {
            return Result.Failure<ApplicationDto>($"Application with name '{request.Name}' not found");
        }

        return Result.Success(new ApplicationDto
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
        });
    }
}
