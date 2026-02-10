using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get compliance evaluations for a specific application
/// </summary>
public record GetComplianceEvaluationsByApplicationQuery : IRequest<Result<IReadOnlyList<ComplianceEvaluationDto>>>
{
    public required Guid ApplicationId { get; init; }
    public string? Environment { get; init; }
    public int Days { get; init; } = 7;
}
