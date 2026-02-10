namespace ComplianceService.Application.DTOs;

/// <summary>
/// Output structure from OPA policy evaluation
/// This is the JSON response received from OPA
/// </summary>
public class OpaDecisionDto
{
    /// <summary>
    /// Whether the policy allows the deployment
    /// </summary>
    public required bool Allow { get; init; }

    /// <summary>
    /// List of policy violations (empty if allowed)
    /// </summary>
    public required IReadOnlyList<PolicyViolationDto> Violations { get; init; }

    /// <summary>
    /// Human-readable decision reason
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Complete OPA result payload for audit trail
    /// </summary>
    public Dictionary<string, object>? RawResult { get; init; }
}

/// <summary>
/// Individual policy violation details
/// </summary>
public class PolicyViolationDto
{
    /// <summary>
    /// Policy rule that was violated
    /// </summary>
    public required string Rule { get; init; }

    /// <summary>
    /// Violation message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Severity of the violation
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Additional violation details
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }
}
