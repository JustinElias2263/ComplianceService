using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Commands;

/// <summary>
/// Command to update an application's risk tier
/// </summary>
public record UpdateApplicationRiskTierCommand : IRequest<Result<ApplicationDto>>
{
    public required Guid ApplicationId { get; init; }
    public required string RiskTier { get; init; }
}
