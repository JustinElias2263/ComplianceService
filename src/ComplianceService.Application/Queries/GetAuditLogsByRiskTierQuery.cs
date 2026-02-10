using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get audit logs by risk tier
/// </summary>
public record GetAuditLogsByRiskTierQuery : IRequest<Result<IReadOnlyList<AuditLogDto>>>
{
    public required string RiskTier { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}
