using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get blocked decisions (denied deployments)
/// </summary>
public record GetBlockedDecisionsQuery : IRequest<Result<IReadOnlyList<AuditLogDto>>>
{
    public DateTime? Since { get; init; }
    public int? Limit { get; init; }
}
