using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.Evaluation.ValueObjects;

/// <summary>
/// Scan results from a security tool (Snyk, Prisma Cloud, etc.)
/// </summary>
public sealed class ScanResult : ValueObject
{
    public string Tool { get; }
    public string ToolVersion { get; }
    public DateTime ScanDate { get; }
    public string? ProjectId { get; }
    public List<Vulnerability> Vulnerabilities { get; }

    private ScanResult(
        string tool,
        string toolVersion,
        DateTime scanDate,
        string? projectId,
        List<Vulnerability> vulnerabilities)
    {
        Tool = tool;
        ToolVersion = toolVersion;
        ScanDate = scanDate;
        ProjectId = projectId;
        Vulnerabilities = vulnerabilities;
    }

    public static Result<ScanResult> Create(
        string tool,
        string toolVersion,
        DateTime scanDate,
        List<Vulnerability> vulnerabilities,
        string? projectId = null)
    {
        if (string.IsNullOrWhiteSpace(tool))
            return Result.Failure<ScanResult>("Tool name cannot be empty");

        if (string.IsNullOrWhiteSpace(toolVersion))
            return Result.Failure<ScanResult>("Tool version cannot be empty");

        if (scanDate > DateTime.UtcNow.AddDays(1))
            return Result.Failure<ScanResult>("Scan date cannot be in the future");

        if (vulnerabilities == null)
            vulnerabilities = new List<Vulnerability>();

        return Result.Success(new ScanResult(
            tool.Trim().ToLowerInvariant(),
            toolVersion.Trim(),
            scanDate,
            projectId?.Trim(),
            vulnerabilities));
    }

    public int CriticalCount => Vulnerabilities.Count(v => v.IsCritical);
    public int HighCount => Vulnerabilities.Count(v => v.Severity.Value == "high");
    public int MediumCount => Vulnerabilities.Count(v => v.Severity.Value == "medium");
    public int LowCount => Vulnerabilities.Count(v => v.Severity.Value == "low");
    public int TotalCount => Vulnerabilities.Count;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Tool;
        yield return ScanDate;
        yield return ProjectId;
        yield return TotalCount;
    }
}
