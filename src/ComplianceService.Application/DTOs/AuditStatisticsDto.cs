namespace ComplianceService.Application.DTOs;

/// <summary>
/// Audit statistics DTO for reporting
/// </summary>
public class AuditStatisticsDto
{
    public required int TotalEvaluations { get; init; }
    public required int AllowedCount { get; init; }
    public required int BlockedCount { get; init; }
    public required double BlockedPercentage { get; init; }
    public required int TotalCriticalVulnerabilities { get; init; }
    public required int TotalHighVulnerabilities { get; init; }
    public required Dictionary<string, int> EvaluationsByEnvironment { get; init; }
    public required Dictionary<string, int> EvaluationsByRiskTier { get; init; }
}
