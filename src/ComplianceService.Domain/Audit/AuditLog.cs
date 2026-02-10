using ComplianceService.Domain.Audit.ValueObjects;
using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.Audit;

/// <summary>
/// Audit Log Aggregate Root
/// Complete record of a compliance decision for audit and compliance purposes
/// </summary>
public class AuditLog : AggregateRoot<Guid>
{
    public string EvaluationId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string ApplicationName { get; private set; }
    public string Environment { get; private set; }
    public string RiskTier { get; private set; }
    public bool Allowed { get; private set; }
    public string Reason { get; private set; }
    public List<string> Violations { get; private set; }
    public DecisionEvidence Evidence { get; private set; }
    public int EvaluationDurationMs { get; private set; }
    public DateTime EvaluatedAt { get; private set; }

    // Aggregated vulnerability counts for quick querying
    public int CriticalCount { get; private set; }
    public int HighCount { get; private set; }
    public int MediumCount { get; private set; }
    public int LowCount { get; private set; }
    public int TotalVulnerabilityCount { get; private set; }

    private AuditLog() : base()
    {
        EvaluationId = string.Empty;
        ApplicationName = string.Empty;
        Environment = string.Empty;
        RiskTier = string.Empty;
        Reason = string.Empty;
        Violations = new List<string>();
        Evidence = null!;
    }

    private AuditLog(
        Guid id,
        string evaluationId,
        Guid applicationId,
        string applicationName,
        string environment,
        string riskTier,
        bool allowed,
        string reason,
        List<string> violations,
        DecisionEvidence evidence,
        int evaluationDurationMs,
        int criticalCount,
        int highCount,
        int mediumCount,
        int lowCount,
        DateTime evaluatedAt) : base(id)
    {
        EvaluationId = evaluationId;
        ApplicationId = applicationId;
        ApplicationName = applicationName;
        Environment = environment;
        RiskTier = riskTier;
        Allowed = allowed;
        Reason = reason;
        Violations = violations;
        Evidence = evidence;
        EvaluationDurationMs = evaluationDurationMs;
        CriticalCount = criticalCount;
        HighCount = highCount;
        MediumCount = mediumCount;
        LowCount = lowCount;
        TotalVulnerabilityCount = criticalCount + highCount + mediumCount + lowCount;
        EvaluatedAt = evaluatedAt;
    }

    public static Result<AuditLog> Create(
        string evaluationId,
        Guid applicationId,
        string applicationName,
        string environment,
        string riskTier,
        bool allowed,
        string reason,
        List<string> violations,
        DecisionEvidence evidence,
        int evaluationDurationMs,
        int criticalCount,
        int highCount,
        int mediumCount,
        int lowCount,
        DateTime evaluatedAt)
    {
        if (string.IsNullOrWhiteSpace(evaluationId))
            return Result.Failure<AuditLog>("Evaluation ID cannot be empty");

        if (applicationId == Guid.Empty)
            return Result.Failure<AuditLog>("Application ID cannot be empty");

        if (string.IsNullOrWhiteSpace(applicationName))
            return Result.Failure<AuditLog>("Application name cannot be empty");

        if (string.IsNullOrWhiteSpace(environment))
            return Result.Failure<AuditLog>("Environment cannot be empty");

        if (string.IsNullOrWhiteSpace(riskTier))
            return Result.Failure<AuditLog>("Risk tier cannot be empty");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<AuditLog>("Reason cannot be empty");

        if (evidence == null)
            return Result.Failure<AuditLog>("Evidence is required");

        if (evaluationDurationMs < 0)
            return Result.Failure<AuditLog>("Evaluation duration cannot be negative");

        if (criticalCount < 0 || highCount < 0 || mediumCount < 0 || lowCount < 0)
            return Result.Failure<AuditLog>("Vulnerability counts cannot be negative");

        return Result.Success(new AuditLog(
            Guid.NewGuid(),
            evaluationId.Trim(),
            applicationId,
            applicationName.Trim(),
            environment.Trim().ToLowerInvariant(),
            riskTier.Trim().ToLowerInvariant(),
            allowed,
            reason.Trim(),
            violations ?? new List<string>(),
            evidence,
            evaluationDurationMs,
            criticalCount,
            highCount,
            mediumCount,
            lowCount,
            evaluatedAt));
    }

    public bool IsBlocked => !Allowed;
    public bool HasCriticalVulnerabilities => CriticalCount > 0;
    public bool HasHighOrCriticalVulnerabilities => CriticalCount > 0 || HighCount > 0;
}
