using ComplianceService.Domain.Audit;
using ComplianceService.Domain.Audit.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.Audit;

public class AuditLogTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var evaluationId = "eval-001";
        var applicationId = Guid.NewGuid();
        var applicationName = "MyService";
        var environment = "production";
        var riskTier = "critical";
        var allowed = true;
        var reason = "All checks passed";
        var violations = new List<string>();
        var evidence = CreateValidEvidence();
        var evaluationDurationMs = 250;
        var evaluatedAt = DateTime.UtcNow;

        // Act
        var result = AuditLog.Create(
            evaluationId,
            applicationId,
            applicationName,
            environment,
            riskTier,
            allowed,
            reason,
            violations,
            evidence,
            evaluationDurationMs,
            criticalCount: 0,
            highCount: 0,
            mediumCount: 0,
            lowCount: 0,
            evaluatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EvaluationId.Should().Be(evaluationId);
        result.Value.ApplicationId.Should().Be(applicationId);
        result.Value.ApplicationName.Should().Be(applicationName);
        result.Value.Environment.Should().Be(environment);
        result.Value.RiskTier.Should().Be(riskTier);
        result.Value.Allowed.Should().Be(allowed);
        result.Value.IsBlocked.Should().BeFalse();
        result.Value.TotalVulnerabilityCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidEvaluationId_ShouldFail(string invalidId)
    {
        // Arrange
        var evidence = CreateValidEvidence();

        // Act
        var result = AuditLog.Create(
            invalidId,
            Guid.NewGuid(),
            "MyService",
            "production",
            "critical",
            true,
            "All checks passed",
            new List<string>(),
            evidence,
            250,
            0, 0, 0, 0,
            DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Evaluation ID");
    }

    [Fact]
    public void Create_WithEmptyApplicationId_ShouldFail()
    {
        // Arrange
        var evidence = CreateValidEvidence();

        // Act
        var result = AuditLog.Create(
            "eval-001",
            Guid.Empty,
            "MyService",
            "production",
            "critical",
            true,
            "All checks passed",
            new List<string>(),
            evidence,
            250,
            0, 0, 0, 0,
            DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Application ID");
    }

    [Fact]
    public void Create_WithNegativeVulnerabilityCounts_ShouldFail()
    {
        // Arrange
        var evidence = CreateValidEvidence();

        // Act
        var result = AuditLog.Create(
            "eval-001",
            Guid.NewGuid(),
            "MyService",
            "production",
            "critical",
            true,
            "All checks passed",
            new List<string>(),
            evidence,
            250,
            criticalCount: -1,
            highCount: 0,
            mediumCount: 0,
            lowCount: 0,
            DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be negative");
    }

    [Fact]
    public void Create_WithNegativeDuration_ShouldFail()
    {
        // Arrange
        var evidence = CreateValidEvidence();

        // Act
        var result = AuditLog.Create(
            "eval-001",
            Guid.NewGuid(),
            "MyService",
            "production",
            "critical",
            true,
            "All checks passed",
            new List<string>(),
            evidence,
            evaluationDurationMs: -100,
            criticalCount: 0,
            highCount: 0,
            mediumCount: 0,
            lowCount: 0,
            DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("duration");
    }

    [Fact]
    public void TotalVulnerabilityCount_ShouldSumAllSeverities()
    {
        // Arrange
        var evidence = CreateValidEvidence();
        var auditLog = AuditLog.Create(
            "eval-001",
            Guid.NewGuid(),
            "MyService",
            "production",
            "critical",
            false,
            "Vulnerabilities detected",
            new List<string> { "Critical vulnerabilities found" },
            evidence,
            250,
            criticalCount: 2,
            highCount: 5,
            mediumCount: 10,
            lowCount: 20,
            DateTime.UtcNow).Value;

        // Act
        var total = auditLog.TotalVulnerabilityCount;

        // Assert
        total.Should().Be(37);
    }

    [Fact]
    public void HasCriticalVulnerabilities_WhenCriticalCountGreaterThanZero_ShouldBeTrue()
    {
        // Arrange
        var evidence = CreateValidEvidence();
        var auditLog = AuditLog.Create(
            "eval-001",
            Guid.NewGuid(),
            "MyService",
            "production",
            "critical",
            false,
            "Critical vulnerabilities detected",
            new List<string>(),
            evidence,
            250,
            criticalCount: 1,
            highCount: 0,
            mediumCount: 0,
            lowCount: 0,
            DateTime.UtcNow).Value;

        // Act & Assert
        auditLog.HasCriticalVulnerabilities.Should().BeTrue();
    }

    [Fact]
    public void HasHighOrCriticalVulnerabilities_WithHighCount_ShouldBeTrue()
    {
        // Arrange
        var evidence = CreateValidEvidence();
        var auditLog = AuditLog.Create(
            "eval-001",
            Guid.NewGuid(),
            "MyService",
            "production",
            "critical",
            false,
            "High vulnerabilities detected",
            new List<string>(),
            evidence,
            250,
            criticalCount: 0,
            highCount: 3,
            mediumCount: 0,
            lowCount: 0,
            DateTime.UtcNow).Value;

        // Act & Assert
        auditLog.HasHighOrCriticalVulnerabilities.Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_WhenNotAllowed_ShouldBeTrue()
    {
        // Arrange
        var evidence = CreateValidEvidence();
        var auditLog = AuditLog.Create(
            "eval-001",
            Guid.NewGuid(),
            "MyService",
            "production",
            "critical",
            allowed: false,
            "Policy violations detected",
            new List<string> { "Critical vulnerabilities found" },
            evidence,
            250,
            criticalCount: 1,
            highCount: 0,
            mediumCount: 0,
            lowCount: 0,
            DateTime.UtcNow).Value;

        // Act & Assert
        auditLog.IsBlocked.Should().BeTrue();
        auditLog.Allowed.Should().BeFalse();
    }

    private static DecisionEvidence CreateValidEvidence()
    {
        return DecisionEvidence.Create(
            "{\"tool\":\"snyk\"}",
            "{\"application\":\"test\"}",
            "{\"allow\":true}").Value;
    }
}
