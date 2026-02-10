namespace ComplianceService.Application.DTOs;

/// <summary>
/// Input structure for OPA policy evaluation
/// This is the JSON payload sent to OPA for decision making
/// </summary>
public class OpaInputDto
{
    /// <summary>
    /// Application context information
    /// </summary>
    public required ApplicationContextDto Application { get; init; }

    /// <summary>
    /// Scan results from security tools (Snyk, Prisma Cloud, etc.)
    /// </summary>
    public required IReadOnlyList<ScanResultDto> ScanResults { get; init; }

    /// <summary>
    /// Additional metadata for policy evaluation
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Application context for OPA evaluation
/// </summary>
public class ApplicationContextDto
{
    public required string Name { get; init; }
    public required string Environment { get; init; }
    public required string RiskTier { get; init; }
    public required string Owner { get; init; }
}
