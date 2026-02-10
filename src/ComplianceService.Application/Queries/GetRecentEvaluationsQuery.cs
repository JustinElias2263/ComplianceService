using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get recent compliance evaluations across all applications
/// </summary>
public record GetRecentEvaluationsQuery : IRequest<Result<IReadOnlyList<ComplianceEvaluationDto>>>
{
    public int Days { get; init; } = 7;
}
