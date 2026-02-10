using ComplianceService.Domain.Evaluation;
using ComplianceService.Domain.Evaluation.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.Evaluation;

public class ComplianceEvaluationTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var environment = "production";
        var riskTier = "critical";
        var scanResults = new List<ScanResult>
        {
            CreateValidScanResult()
        };
        var decision = CreateValidPolicyDecision(allowed: true);

        // Act
        var result = ComplianceEvaluation.Create(
            applicationId,
            environment,
            riskTier,
            scanResults,
            decision);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ApplicationId.Should().Be(applicationId);
        result.Value.Environment.Should().Be(environment);
        result.Value.RiskTier.Should().Be(riskTier);
        result.Value.ScanResults.Should().HaveCount(1);
        result.Value.Decision.Should().Be(decision);
        result.Value.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyApplicationId_ShouldFail()
    {
        // Arrange
        var applicationId = Guid.Empty;
        var scanResults = new List<ScanResult> { CreateValidScanResult() };
        var decision = CreateValidPolicyDecision(allowed: true);

        // Act
        var result = ComplianceEvaluation.Create(
            applicationId,
            "production",
            "critical",
            scanResults,
            decision);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Application ID");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidEnvironment_ShouldFail(string invalidEnvironment)
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var scanResults = new List<ScanResult> { CreateValidScanResult() };
        var decision = CreateValidPolicyDecision(allowed: true);

        // Act
        var result = ComplianceEvaluation.Create(
            applicationId,
            invalidEnvironment,
            "critical",
            scanResults,
            decision);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Environment");
    }

    [Fact]
    public void Create_WithNoScanResults_ShouldFail()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var scanResults = new List<ScanResult>();
        var decision = CreateValidPolicyDecision(allowed: true);

        // Act
        var result = ComplianceEvaluation.Create(
            applicationId,
            "production",
            "critical",
            scanResults,
            decision);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("scan result");
    }

    [Fact]
    public void Create_WithNullDecision_ShouldFail()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var scanResults = new List<ScanResult> { CreateValidScanResult() };

        // Act
        var result = ComplianceEvaluation.Create(
            applicationId,
            "production",
            "critical",
            scanResults,
            null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("decision");
    }

    [Fact]
    public void GetCriticalVulnerabilityCount_ShouldSumAllScanResults()
    {
        // Arrange
        var scanResults = new List<ScanResult>
        {
            ScanResult.Create("snyk", "1.0", DateTime.UtcNow, new List<Vulnerability>
            {
                CreateVulnerability("critical"),
                CreateVulnerability("critical")
            }).Value,
            ScanResult.Create("prismacloud", "1.0", DateTime.UtcNow, new List<Vulnerability>
            {
                CreateVulnerability("critical")
            }).Value
        };
        var decision = CreateValidPolicyDecision(allowed: false);
        var evaluation = ComplianceEvaluation.Create(
            Guid.NewGuid(),
            "production",
            "critical",
            scanResults,
            decision).Value;

        // Act
        var count = evaluation.GetCriticalVulnerabilityCount();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void GetTotalVulnerabilityCount_ShouldSumAllSeverities()
    {
        // Arrange
        var scanResults = new List<ScanResult>
        {
            ScanResult.Create("snyk", "1.0", DateTime.UtcNow, new List<Vulnerability>
            {
                CreateVulnerability("critical"),
                CreateVulnerability("high"),
                CreateVulnerability("medium"),
                CreateVulnerability("low")
            }).Value
        };
        var decision = CreateValidPolicyDecision(allowed: false);
        var evaluation = ComplianceEvaluation.Create(
            Guid.NewGuid(),
            "production",
            "critical",
            scanResults,
            decision).Value;

        // Act
        var count = evaluation.GetTotalVulnerabilityCount();

        // Assert
        count.Should().Be(4);
    }

    [Fact]
    public void IsBlocked_WhenNotAllowed_ShouldBeTrue()
    {
        // Arrange
        var scanResults = new List<ScanResult> { CreateValidScanResult() };
        var decision = CreateValidPolicyDecision(allowed: false);
        var evaluation = ComplianceEvaluation.Create(
            Guid.NewGuid(),
            "production",
            "critical",
            scanResults,
            decision).Value;

        // Act & Assert
        evaluation.IsBlocked.Should().BeTrue();
        evaluation.IsAllowed.Should().BeFalse();
    }

    private static ScanResult CreateValidScanResult()
    {
        return ScanResult.Create(
            "snyk",
            "1.0",
            DateTime.UtcNow,
            new List<Vulnerability>
            {
                CreateVulnerability("high")
            }).Value;
    }

    private static Vulnerability CreateVulnerability(string severity)
    {
        return Vulnerability.Create(
            $"VULN-{Guid.NewGuid().ToString()[..8]}",
            "Test Vulnerability",
            severity,
            7.5,
            "test-package",
            "1.0.0",
            "1.0.1").Value;
    }

    private static PolicyDecision CreateValidPolicyDecision(bool allowed)
    {
        var violations = allowed
            ? new List<string>()
            : new List<string> { "Critical vulnerabilities detected" };

        return PolicyDecision.Create(allowed, violations).Value;
    }
}
