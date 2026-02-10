using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.ApplicationProfile.ValueObjects;

/// <summary>
/// Reference to an OPA policy package
/// Example: "compliance/critical-production"
/// </summary>
public sealed class PolicyReference : ValueObject
{
    public string PackageName { get; }

    private PolicyReference(string packageName)
    {
        PackageName = packageName;
    }

    public static Result<PolicyReference> Create(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return Result.Failure<PolicyReference>("Policy package name cannot be empty");

        var trimmed = packageName.Trim();

        // Validate format: should be like "compliance/policy-name" or "compliance.policy_name"
        if (!trimmed.Contains('/') && !trimmed.Contains('.'))
            return Result.Failure<PolicyReference>(
                "Policy reference must contain a package separator (/ or .)");

        if (trimmed.Length < 3 || trimmed.Length > 200)
            return Result.Failure<PolicyReference>(
                "Policy reference must be between 3 and 200 characters");

        return Result.Success(new PolicyReference(trimmed));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PackageName;
    }

    public override string ToString() => PackageName;
}
