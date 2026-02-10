using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get audit logs with critical vulnerabilities
/// </summary>
public record GetCriticalVulnerabilitiesQuery : IRequest<Result<IReadOnlyList<AuditLogDto>>>
{
    public DateTime? Since { get; init; }
}
