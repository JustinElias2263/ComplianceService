using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get blocked compliance evaluations (denied deployments)
/// </summary>
public record GetBlockedEvaluationsQuery : IRequest<Result<IReadOnlyList<ComplianceEvaluationDto>>>
{
    public DateTime? Since { get; init; }
}
