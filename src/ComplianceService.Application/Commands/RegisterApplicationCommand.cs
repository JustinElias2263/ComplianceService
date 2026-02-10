using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Commands;

/// <summary>
/// Command to register a new application in the compliance system
/// </summary>
public record RegisterApplicationCommand : IRequest<Result<ApplicationDto>>
{
    public required string Name { get; init; }
    public required string RiskTier { get; init; }
    public required string Owner { get; init; }
}
