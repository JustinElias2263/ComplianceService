using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.ApplicationProfile.ValueObjects;

/// <summary>
/// Represents the risk classification of an application
/// Determines which compliance policies apply
/// </summary>
public sealed class RiskTier : ValueObject
{
    public string Value { get; }

    // Predefined risk tiers
    public static readonly RiskTier Critical = new("critical");
    public static readonly RiskTier High = new("high");
    public static readonly RiskTier Medium = new("medium");
    public static readonly RiskTier Low = new("low");

    private RiskTier(string value)
    {
        Value = value;
    }

    public static Result<RiskTier> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<RiskTier>("Risk tier cannot be empty");

        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "critical" => Result.Success(Critical),
            "high" => Result.Success(High),
            "medium" => Result.Success(Medium),
            "low" => Result.Success(Low),
            _ => Result.Failure<RiskTier>($"Invalid risk tier: {value}. Must be critical, high, medium, or low")
        };
    }

    public bool IsCritical => Value == "critical";
    public bool IsHighOrAbove => Value is "critical" or "high";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
