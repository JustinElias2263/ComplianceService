using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;

namespace ComplianceService.Api.Tests.Fixtures;

/// <summary>
/// Generates realistic test data for compliance scenarios
/// </summary>
public static class TestDataGenerator
{
    public static RegisterApplicationCommand CreateProductionApplication(string name = "ProductionApp")
    {
        return new RegisterApplicationCommand
        {
            Name = name,
            Owner = "platform-team@company.com"
        };
    }

    public static RegisterApplicationCommand CreateStagingApplication(string name = "StagingApp")
    {
        return new RegisterApplicationCommand
        {
            Name = name,
            Owner = "qa-team@company.com"
        };
    }

    public static RegisterApplicationCommand CreateDevelopmentApplication(string name = "DevApp")
    {
        return new RegisterApplicationCommand
        {
            Name = name,
            Owner = "dev-team@company.com"
        };
    }

    public static AddEnvironmentConfigCommand CreateProductionEnvironment(Guid applicationId)
    {
        return new AddEnvironmentConfigCommand
        {
            ApplicationId = applicationId,
            EnvironmentName = "production",
            RiskTier = "critical",
            SecurityTools = new List<string> { "snyk", "prismacloud" },
            PolicyReferences = new List<string> { "compliance/cicd/production" }
        };
    }

    public static AddEnvironmentConfigCommand CreateStagingEnvironment(Guid applicationId)
    {
        return new AddEnvironmentConfigCommand
        {
            ApplicationId = applicationId,
            EnvironmentName = "staging",
            RiskTier = "high",
            SecurityTools = new List<string> { "snyk" },
            PolicyReferences = new List<string> { "compliance/cicd/staging" }
        };
    }

    public static AddEnvironmentConfigCommand CreateDevelopmentEnvironment(Guid applicationId)
    {
        return new AddEnvironmentConfigCommand
        {
            ApplicationId = applicationId,
            EnvironmentName = "development",
            RiskTier = "low",
            SecurityTools = new List<string> { "snyk" },
            PolicyReferences = new List<string> { "compliance/cicd/development" }
        };
    }

    /// <summary>
    /// Creates a clean scan with no vulnerabilities
    /// </summary>
    public static EvaluateComplianceCommand CreateCleanScan(Guid applicationId, string environment = "production")
    {
        return new EvaluateComplianceCommand
        {
            ApplicationId = applicationId,
            Environment = environment,
            InitiatedBy = "test-pipeline",
            ScanResults = new List<ScanResultDto>
            {
                new ScanResultDto
                {
                    ToolName = "snyk",
                    ScannedAt = DateTime.UtcNow,
                    RawOutput = "{}",
                    Vulnerabilities = new List<VulnerabilityDto>()
                }
            }
        };
    }

    /// <summary>
    /// Creates a scan with critical vulnerabilities (should block production)
    /// </summary>
    public static EvaluateComplianceCommand CreateScanWithCriticalVulnerabilities(
        Guid applicationId,
        string environment = "production",
        int criticalCount = 2)
    {
        var vulnerabilities = new List<VulnerabilityDto>();

        for (int i = 0; i < criticalCount; i++)
        {
            vulnerabilities.Add(new VulnerabilityDto
            {
                CveId = $"CVE-2024-{1000 + i}",
                Description = $"Critical vulnerability {i + 1}: Remote code execution in authentication module",
                Severity = "critical",
                CvssScore = 9.8m,
                PackageName = $"vulnerable-package-{i}",
                CurrentVersion = "1.0.0",
                FixedVersion = "1.0.1",
                IsFixable = true,
                Source = "snyk"
            });
        }

        return new EvaluateComplianceCommand
        {
            ApplicationId = applicationId,
            Environment = environment,
            InitiatedBy = "test-pipeline",
            ScanResults = new List<ScanResultDto>
            {
                new ScanResultDto
                {
                    ToolName = "snyk",
                    ScannedAt = DateTime.UtcNow,
                    RawOutput = "{}",
                    Vulnerabilities = vulnerabilities
                }
            }
        };
    }

    /// <summary>
    /// Creates a scan with high vulnerabilities (blocks production, may allow staging)
    /// </summary>
    public static EvaluateComplianceCommand CreateScanWithHighVulnerabilities(
        Guid applicationId,
        string environment = "staging",
        int highCount = 2)
    {
        var vulnerabilities = new List<VulnerabilityDto>();

        for (int i = 0; i < highCount; i++)
        {
            vulnerabilities.Add(new VulnerabilityDto
            {
                CveId = $"HIGH-{1000 + i}",
                Description = $"High severity vulnerability {i + 1}: SQL injection in query builder",
                Severity = "high",
                CvssScore = 7.5m,
                PackageName = $"high-vuln-package-{i}",
                CurrentVersion = "2.0.0",
                FixedVersion = "2.1.0",
                IsFixable = true,
                Source = "snyk"
            });
        }

        return new EvaluateComplianceCommand
        {
            ApplicationId = applicationId,
            Environment = environment,
            InitiatedBy = "test-pipeline",
            ScanResults = new List<ScanResultDto>
            {
                new ScanResultDto
                {
                    ToolName = "snyk",
                    ScannedAt = DateTime.UtcNow,
                    RawOutput = "{}",
                    Vulnerabilities = vulnerabilities
                }
            }
        };
    }

    /// <summary>
    /// Creates a scan with only medium/low vulnerabilities (should allow all environments)
    /// </summary>
    public static EvaluateComplianceCommand CreateScanWithMediumLowVulnerabilities(
        Guid applicationId,
        string environment = "development")
    {
        var vulnerabilities = new List<VulnerabilityDto>
        {
            new VulnerabilityDto
            {
                CveId = "MED-1001",
                Description = "Medium vulnerability: Information disclosure in error messages",
                Severity = "medium",
                CvssScore = 5.3m,
                PackageName = "logger-package",
                CurrentVersion = "3.0.0",
                FixedVersion = "3.1.0",
                IsFixable = true,
                Source = "snyk"
            },
            new VulnerabilityDto
            {
                CveId = "LOW-1001",
                Description = "Low vulnerability: Missing security headers",
                Severity = "low",
                CvssScore = 3.1m,
                PackageName = "web-framework",
                CurrentVersion = "4.0.0",
                FixedVersion = "4.0.1",
                IsFixable = true,
                Source = "snyk"
            }
        };

        return new EvaluateComplianceCommand
        {
            ApplicationId = applicationId,
            Environment = environment,
            InitiatedBy = "test-pipeline",
            ScanResults = new List<ScanResultDto>
            {
                new ScanResultDto
                {
                    ToolName = "snyk",
                    ScannedAt = DateTime.UtcNow,
                    RawOutput = "{}",
                    Vulnerabilities = vulnerabilities
                }
            }
        };
    }

    /// <summary>
    /// Creates a scan with multiple tools (Snyk + Prisma Cloud)
    /// </summary>
    public static EvaluateComplianceCommand CreateMultiToolScan(Guid applicationId, string environment = "production")
    {
        return new EvaluateComplianceCommand
        {
            ApplicationId = applicationId,
            Environment = environment,
            InitiatedBy = "test-pipeline",
            ScanResults = new List<ScanResultDto>
            {
                new ScanResultDto
                {
                    ToolName = "snyk",
                    ScannedAt = DateTime.UtcNow,
                    RawOutput = "{}",
                    Vulnerabilities = new List<VulnerabilityDto>
                    {
                        new VulnerabilityDto
                        {
                            CveId = "SNYK-001",
                            Description = "Dependency vulnerability detected by Snyk",
                            Severity = "medium",
                            CvssScore = 5.5m,
                            PackageName = "dependency-a",
                            CurrentVersion = "1.0.0",
                            FixedVersion = "1.1.0",
                            IsFixable = true,
                            Source = "snyk"
                        }
                    }
                },
                new ScanResultDto
                {
                    ToolName = "prismacloud",
                    ScannedAt = DateTime.UtcNow,
                    RawOutput = "{}",
                    Vulnerabilities = new List<VulnerabilityDto>
                    {
                        new VulnerabilityDto
                        {
                            CveId = "PRISMA-001",
                            Description = "Container vulnerability detected by Prisma Cloud",
                            Severity = "medium",
                            CvssScore = 4.8m,
                            PackageName = "base-image",
                            CurrentVersion = "alpine:3.14",
                            FixedVersion = "alpine:3.18",
                            IsFixable = true,
                            Source = "prismacloud"
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a realistic production deployment scenario
    /// </summary>
    public static EvaluateComplianceCommand CreateRealisticProductionDeployment(Guid applicationId)
    {
        var vulnerabilities = new List<VulnerabilityDto>
        {
            // Some fixed vulnerabilities (low severity)
            new VulnerabilityDto
            {
                CveId = "CVE-2023-1234",
                Description = "Outdated logging library - low risk",
                Severity = "low",
                CvssScore = 2.1m,
                PackageName = "logging-lib",
                CurrentVersion = "1.0.0",
                FixedVersion = "1.0.1",
                IsFixable = true,
                Source = "snyk"
            },
            new VulnerabilityDto
            {
                CveId = "CVE-2023-5678",
                Description = "Minor configuration issue",
                Severity = "low",
                CvssScore = 1.9m,
                PackageName = "config-parser",
                CurrentVersion = "2.0.0",
                FixedVersion = "2.0.1",
                IsFixable = true,
                Source = "snyk"
            }
        };

        return new EvaluateComplianceCommand
        {
            ApplicationId = applicationId,
            Environment = "production",
            InitiatedBy = "test-pipeline",
            ScanResults = new List<ScanResultDto>
            {
                new ScanResultDto
                {
                    ToolName = "snyk",
                    ScannedAt = DateTime.UtcNow,
                    RawOutput = "{}",
                    Vulnerabilities = vulnerabilities
                }
            }
        };
    }
}
