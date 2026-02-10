using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get audit logs with filtering
/// </summary>
public record GetAuditLogsQuery : IRequest<Result<IReadOnlyList<AuditLogDto>>>
{
    /// <summary>
    /// Filter by application name (optional)
    /// </summary>
    public string? ApplicationName { get; init; }

    /// <summary>
    /// Filter by environment (optional)
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>
    /// Filter by decision (optional)
    /// </summary>
    public bool? DecisionAllow { get; init; }

    /// <summary>
    /// Start date for date range filter (optional)
    /// </summary>
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// End date for date range filter (optional)
    /// </summary>
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// Page number for pagination (starts at 1)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Page size for pagination
    /// </summary>
    public int PageSize { get; init; } = 50;
}
