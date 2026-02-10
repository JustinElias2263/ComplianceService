namespace ComplianceService.Application.DTOs;

/// <summary>
/// Audit log entry DTO
/// </summary>
public class AuditLogDto
{
    public required Guid Id { get; init; }
    public required Guid EvaluationId { get; init; }
    public required string ApplicationName { get; init; }
    public required string Environment { get; init; }
    public required DateTime Timestamp { get; init; }
    public required bool DecisionAllow { get; init; }
    public required IReadOnlyList<string> Violations { get; init; }
    public required string PolicyPackage { get; init; }
    public required int TotalVulnerabilities { get; init; }
    public required int CriticalCount { get; init; }
    public required int HighCount { get; init; }
    public required string InitiatedBy { get; init; }
    public required string CompleteEvidence { get; init; } // JSON evidence
}
