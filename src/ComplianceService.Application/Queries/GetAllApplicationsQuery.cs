using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get all applications with optional filtering
/// </summary>
public record GetAllApplicationsQuery : IRequest<Result<IReadOnlyList<ApplicationDto>>>
{
    /// <summary>
    /// Filter by risk tier (optional)
    /// </summary>
    public string? RiskTier { get; init; }

    /// <summary>
    /// Filter by owner (optional)
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Page number for pagination (starts at 1)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Page size for pagination
    /// </summary>
    public int PageSize { get; init; } = 50;
}
