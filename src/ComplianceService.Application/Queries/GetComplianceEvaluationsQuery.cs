using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get compliance evaluations with filtering
/// </summary>
public record GetComplianceEvaluationsQuery : IRequest<Result<IReadOnlyList<ComplianceEvaluationDto>>>
{
    /// <summary>
    /// Filter by application ID (optional)
    /// </summary>
    public Guid? ApplicationId { get; init; }

    /// <summary>
    /// Filter by environment (optional)
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>
    /// Filter by passed/failed status (optional)
    /// </summary>
    public bool? Passed { get; init; }

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
