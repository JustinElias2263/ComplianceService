using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.ApplicationProfile.Entities;

/// <summary>
/// Configuration for a specific environment (production, staging, dev)
/// Defines which security tools and policies apply
/// </summary>
public class EnvironmentConfig : Entity<Guid>
{
    public Guid ApplicationId { get; private set; }
    public string EnvironmentName { get; private set; }
    public RiskTier RiskTier { get; private set; }
    public List<SecurityToolType> SecurityTools { get; private set; }
    public List<PolicyReference> Policies { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; }

    // EF Core navigation
    public Application? Application { get; private set; }

    private EnvironmentConfig() : base()
    {
        EnvironmentName = string.Empty;
        RiskTier = ValueObjects.RiskTier.Low;
        SecurityTools = new List<SecurityToolType>();
        Policies = new List<PolicyReference>();
        Metadata = new Dictionary<string, string>();
    }

    private EnvironmentConfig(
        Guid id,
        Guid applicationId,
        string environmentName,
        RiskTier riskTier,
        List<SecurityToolType> securityTools,
        List<PolicyReference> policies,
        Dictionary<string, string> metadata) : base(id)
    {
        ApplicationId = applicationId;
        EnvironmentName = environmentName;
        RiskTier = riskTier;
        SecurityTools = securityTools;
        Policies = policies;
        Metadata = metadata;
    }

    public static Result<EnvironmentConfig> Create(
        Guid applicationId,
        string environmentName,
        RiskTier riskTier,
        List<SecurityToolType> securityTools,
        List<PolicyReference> policies,
        Dictionary<string, string>? metadata = null)
    {
        if (applicationId == Guid.Empty)
            return Result.Failure<EnvironmentConfig>("Application ID cannot be empty");

        if (string.IsNullOrWhiteSpace(environmentName))
            return Result.Failure<EnvironmentConfig>("Environment name cannot be empty");

        var normalized = environmentName.Trim().ToLowerInvariant();
        if (!IsValidEnvironmentName(normalized))
            return Result.Failure<EnvironmentConfig>(
                $"Invalid environment name: {environmentName}. Common names: production, staging, dev, test");

        if (securityTools == null || securityTools.Count == 0)
            return Result.Failure<EnvironmentConfig>("At least one security tool must be configured");

        if (policies == null || policies.Count == 0)
            return Result.Failure<EnvironmentConfig>("At least one policy must be assigned");

        return Result.Success(new EnvironmentConfig(
            Guid.NewGuid(),
            applicationId,
            normalized,
            riskTier,
            securityTools,
            policies,
            metadata ?? new Dictionary<string, string>()));
    }

    private static bool IsValidEnvironmentName(string name)
    {
        var validNames = new[] { "production", "prod", "staging", "stage", "dev", "development", "test", "qa", "uat" };
        return validNames.Contains(name);
    }

    public void UpdateRiskTier(RiskTier newRiskTier)
    {
        RiskTier = newRiskTier;
    }

    public void UpdateSecurityTools(List<SecurityToolType> tools)
    {
        if (tools == null || tools.Count == 0)
            throw new ArgumentException("At least one security tool must be configured");

        SecurityTools = tools;
    }

    public void UpdatePolicies(List<PolicyReference> policies)
    {
        if (policies == null || policies.Count == 0)
            throw new ArgumentException("At least one policy must be assigned");

        Policies = policies;
    }

    public void UpdateMetadata(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key cannot be empty");

        Metadata[key] = value;
    }

    public void RemoveMetadata(string key)
    {
        Metadata.Remove(key);
    }

    public bool IsProduction => EnvironmentName is "production" or "prod";
}
