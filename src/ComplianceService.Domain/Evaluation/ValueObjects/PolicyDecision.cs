using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.Evaluation.ValueObjects;

/// <summary>
/// Decision returned by OPA policy evaluation
/// </summary>
public sealed class PolicyDecision : ValueObject
{
    public bool Allowed { get; }
    public List<string> Violations { get; }
    public Dictionary<string, object> Details { get; }
    public int EvaluationDurationMs { get; }

    private PolicyDecision(
        bool allowed,
        List<string> violations,
        Dictionary<string, object> details,
        int evaluationDurationMs)
    {
        Allowed = allowed;
        Violations = violations;
        Details = details;
        EvaluationDurationMs = evaluationDurationMs;
    }

    public static Result<PolicyDecision> Create(
        bool allowed,
        List<string>? violations = null,
        Dictionary<string, object>? details = null,
        int evaluationDurationMs = 0)
    {
        if (!allowed && (violations == null || violations.Count == 0))
            return Result.Failure<PolicyDecision>("Denied decisions must have at least one violation");

        if (evaluationDurationMs < 0)
            return Result.Failure<PolicyDecision>("Evaluation duration cannot be negative");

        return Result.Success(new PolicyDecision(
            allowed,
            violations ?? new List<string>(),
            details ?? new Dictionary<string, object>(),
            evaluationDurationMs));
    }

    public string GetReason()
    {
        if (Allowed)
            return "All compliance checks passed";

        return Violations.Count == 1
            ? Violations[0]
            : $"{Violations.Count} policy violations found";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Allowed;
        yield return Violations.Count;
        yield return EvaluationDurationMs;
    }
}
