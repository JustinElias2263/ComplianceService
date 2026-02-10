using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Commands;

/// <summary>
/// Command to update an existing environment configuration
/// </summary>
public record UpdateEnvironmentConfigCommand : IRequest<Result<ApplicationDto>>
{
    public required Guid ApplicationId { get; init; }
    public required string EnvironmentName { get; init; }
    public IReadOnlyList<string>? SecurityTools { get; init; }
    public IReadOnlyList<string>? PolicyReferences { get; init; }
    public bool? IsActive { get; init; }
}
