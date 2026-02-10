namespace ComplianceService.Application.DTOs;

/// <summary>
/// Security tool scan result DTO
/// Represents output from Snyk, Prisma Cloud, or other security tools
/// </summary>
public class ScanResultDto
{
    /// <summary>
    /// Tool that generated the scan (e.g., "snyk", "prismacloud")
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// When the scan was performed
    /// </summary>
    public required DateTime ScannedAt { get; init; }

    /// <summary>
    /// List of vulnerabilities found
    /// </summary>
    public required IReadOnlyList<VulnerabilityDto> Vulnerabilities { get; init; }

    /// <summary>
    /// Raw tool output (JSON)
    /// </summary>
    public required string RawOutput { get; init; }

    /// <summary>
    /// Aggregated vulnerability counts by severity
    /// </summary>
    public VulnerabilityCountsDto? Counts { get; init; }
}

/// <summary>
/// Vulnerability counts by severity
/// </summary>
public class VulnerabilityCountsDto
{
    public int Critical { get; init; }
    public int High { get; init; }
    public int Medium { get; init; }
    public int Low { get; init; }
    public int Total { get; init; }
}
