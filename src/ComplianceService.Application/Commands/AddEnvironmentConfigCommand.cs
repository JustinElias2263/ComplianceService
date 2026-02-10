using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Commands;

/// <summary>
/// Command to add an environment configuration to an application
/// </summary>
public record AddEnvironmentConfigCommand : IRequest<Result<ApplicationDto>>
{
    public required Guid ApplicationId { get; init; }
    public required string EnvironmentName { get; init; }
    public required string RiskTier { get; init; }
    public required IReadOnlyList<string> SecurityTools { get; init; }
    public required IReadOnlyList<string> PolicyReferences { get; init; }
}
