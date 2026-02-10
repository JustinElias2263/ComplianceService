using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get audit logs for an application
/// </summary>
public record GetAuditLogsByApplicationQuery : IRequest<Result<IReadOnlyList<AuditLogDto>>>
{
    public required Guid ApplicationId { get; init; }
    public string? Environment { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int PageSize { get; init; } = 50;
    public int PageNumber { get; init; } = 1;
}
