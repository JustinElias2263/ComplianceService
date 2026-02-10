using ComplianceService.Domain.Evaluation.Events;
using ComplianceService.Domain.Evaluation.ValueObjects;
using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.Evaluation;

/// <summary>
/// Compliance Evaluation Aggregate Root
/// Represents the evaluation of scan results against compliance policies
/// </summary>
public class ComplianceEvaluation : AggregateRoot<Guid>
{
    public Guid ApplicationId { get; private set; }
    public string Environment { get; private set; }
    public string RiskTier { get; private set; }
    public List<ScanResult> ScanResults { get; private set; }
    public PolicyDecision Decision { get; private set; }
    public DateTime EvaluatedAt { get; private set; }

    private ComplianceEvaluation() : base()
    {
        Environment = string.Empty;
        RiskTier = string.Empty;
        ScanResults = new List<ScanResult>();
        Decision = null!;
    }

    private ComplianceEvaluation(
        Guid id,
        Guid applicationId,
        string environment,
        string riskTier,
        List<ScanResult> scanResults,
        PolicyDecision decision,
        DateTime evaluatedAt) : base(id)
    {
        ApplicationId = applicationId;
        Environment = environment;
        RiskTier = riskTier;
        ScanResults = scanResults;
        Decision = decision;
        EvaluatedAt = evaluatedAt;
    }

    public static Result<ComplianceEvaluation> Create(
        Guid applicationId,
        string environment,
        string riskTier,
        List<ScanResult> scanResults,
        PolicyDecision decision)
    {
        if (applicationId == Guid.Empty)
            return Result.Failure<ComplianceEvaluation>("Application ID cannot be empty");

        if (string.IsNullOrWhiteSpace(environment))
            return Result.Failure<ComplianceEvaluation>("Environment cannot be empty");

        if (string.IsNullOrWhiteSpace(riskTier))
            return Result.Failure<ComplianceEvaluation>("Risk tier cannot be empty");

        if (scanResults == null || scanResults.Count == 0)
            return Result.Failure<ComplianceEvaluation>("At least one scan result is required");

        if (decision == null)
            return Result.Failure<ComplianceEvaluation>("Policy decision is required");

        var id = Guid.NewGuid();
        var evaluatedAt = DateTime.UtcNow;

        var evaluation = new ComplianceEvaluation(
            id,
            applicationId,
            environment.Trim().ToLowerInvariant(),
            riskTier.Trim().ToLowerInvariant(),
            scanResults,
            decision,
            evaluatedAt);

        evaluation.AddDomainEvent(new ComplianceEvaluationCompletedEvent(
            id,
            applicationId,
            environment,
            decision.Allowed,
            decision.Violations,
            GetTotalVulnerabilityCount(scanResults),
            evaluatedAt));

        return Result.Success(evaluation);
    }

    private static int GetTotalVulnerabilityCount(List<ScanResult> scanResults)
    {
        return scanResults.Sum(sr => sr.TotalCount);
    }

    public int GetCriticalVulnerabilityCount()
    {
        return ScanResults.Sum(sr => sr.CriticalCount);
    }

    public int GetHighVulnerabilityCount()
    {
        return ScanResults.Sum(sr => sr.HighCount);
    }

    public int GetMediumVulnerabilityCount()
    {
        return ScanResults.Sum(sr => sr.MediumCount);
    }

    public int GetLowVulnerabilityCount()
    {
        return ScanResults.Sum(sr => sr.LowCount);
    }

    public int GetTotalVulnerabilityCount()
    {
        return ScanResults.Sum(sr => sr.TotalCount);
    }

    public bool IsAllowed => Decision.Allowed;
    public bool IsBlocked => !Decision.Allowed;
}
