using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Commands;

/// <summary>
/// Command to deactivate an environment configuration for an application
/// </summary>
public record DeactivateEnvironmentCommand : IRequest<Result<ApplicationDto>>
{
    public required Guid ApplicationId { get; init; }
    public required string EnvironmentName { get; init; }
}
