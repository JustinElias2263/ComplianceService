using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.Audit.ValueObjects;

/// <summary>
/// Evidence stored for compliance audit purposes
/// Contains the complete scan results and evaluation details
/// </summary>
public sealed class DecisionEvidence : ValueObject
{
    public string ScanResultsJson { get; }
    public string PolicyInputJson { get; }
    public string PolicyOutputJson { get; }
    public DateTime CapturedAt { get; }

    private DecisionEvidence(
        string scanResultsJson,
        string policyInputJson,
        string policyOutputJson,
        DateTime capturedAt)
    {
        ScanResultsJson = scanResultsJson;
        PolicyInputJson = policyInputJson;
        PolicyOutputJson = policyOutputJson;
        CapturedAt = capturedAt;
    }

    public static Result<DecisionEvidence> Create(
        string scanResultsJson,
        string policyInputJson,
        string policyOutputJson)
    {
        if (string.IsNullOrWhiteSpace(scanResultsJson))
            return Result.Failure<DecisionEvidence>("Scan results JSON cannot be empty");

        if (string.IsNullOrWhiteSpace(policyInputJson))
            return Result.Failure<DecisionEvidence>("Policy input JSON cannot be empty");

        if (string.IsNullOrWhiteSpace(policyOutputJson))
            return Result.Failure<DecisionEvidence>("Policy output JSON cannot be empty");

        return Result.Success(new DecisionEvidence(
            scanResultsJson,
            policyInputJson,
            policyOutputJson,
            DateTime.UtcNow));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ScanResultsJson;
        yield return PolicyInputJson;
        yield return PolicyOutputJson;
        yield return CapturedAt;
    }
}
