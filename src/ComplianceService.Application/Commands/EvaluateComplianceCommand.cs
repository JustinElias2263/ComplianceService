using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Commands;

/// <summary>
/// Command to evaluate compliance for an application deployment
/// This is the main workflow triggered by CI/CD pipelines
/// </summary>
public record EvaluateComplianceCommand : IRequest<Result<ComplianceEvaluationDto>>
{
    /// <summary>
    /// Application identifier
    /// </summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>
    /// Target environment (e.g., "production", "staging")
    /// </summary>
    public required string Environment { get; init; }

    /// <summary>
    /// Scan results from security tools (forwarded from CI pipeline)
    /// </summary>
    public required IReadOnlyList<ScanResultDto> ScanResults { get; init; }

    /// <summary>
    /// User or system that initiated the evaluation
    /// </summary>
    public required string InitiatedBy { get; init; }

    /// <summary>
    /// Additional metadata for policy evaluation
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
