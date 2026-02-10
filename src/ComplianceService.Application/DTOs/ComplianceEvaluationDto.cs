namespace ComplianceService.Application.DTOs;

/// <summary>
/// Compliance evaluation result DTO
/// </summary>
public class ComplianceEvaluationDto
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }
    public required string ApplicationName { get; init; }
    public required string Environment { get; init; }
    public required DateTime EvaluatedAt { get; init; }
    public required bool Passed { get; init; }
    public required IReadOnlyList<ScanResultDto> ScanResults { get; init; }
    public required PolicyDecisionDto PolicyDecision { get; init; }
    public required VulnerabilityCountsDto AggregatedCounts { get; init; }
}

/// <summary>
/// Policy decision summary DTO
/// </summary>
public class PolicyDecisionDto
{
    public required bool Allow { get; init; }
    public required IReadOnlyList<PolicyViolationDto> Violations { get; init; }
    public required string PolicyPackage { get; init; }
    public string? Reason { get; init; }
}
