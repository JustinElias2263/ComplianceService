using ComplianceService.Domain.Evaluation.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.ValueObjects;

public class PolicyDecisionTests
{
    [Fact]
    public void Create_WithAllowedDecision_ShouldSucceed()
    {
        // Act
        var result = PolicyDecision.Create(
            allowed: true,
            violations: new List<string>(),
            details: new Dictionary<string, object>(),
            evaluationDurationMs: 150);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Allowed.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
        result.Value.Details.Should().BeEmpty();
        result.Value.EvaluationDurationMs.Should().Be(150);
    }

    [Fact]
    public void Create_WithDeniedDecisionAndViolations_ShouldSucceed()
    {
        // Arrange
        var violations = new List<string> { "Critical vulnerabilities detected", "Missing required scans" };
        var details = new Dictionary<string, object>
        {
            ["criticalCount"] = 3,
            ["policy"] = "compliance/production"
        };

        // Act
        var result = PolicyDecision.Create(
            allowed: false,
            violations: violations,
            details: details,
            evaluationDurationMs: 200);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Allowed.Should().BeFalse();
        result.Value.Violations.Should().BeEquivalentTo(violations);
        result.Value.Details.Should().ContainKey("criticalCount");
        result.Value.EvaluationDurationMs.Should().Be(200);
    }

    [Fact]
    public void Create_WithAllowedAndNoParameters_ShouldSucceed()
    {
        // Act
        var result = PolicyDecision.Create(allowed: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Allowed.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
        result.Value.Details.Should().BeEmpty();
        result.Value.EvaluationDurationMs.Should().Be(0);
    }

    [Fact]
    public void Create_WithDeniedButNoViolations_ShouldFail()
    {
        // Act
        var result = PolicyDecision.Create(
            allowed: false,
            violations: new List<string>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must have at least one violation");
    }

    [Fact]
    public void Create_WithDeniedAndNullViolations_ShouldFail()
    {
        // Act
        var result = PolicyDecision.Create(
            allowed: false,
            violations: null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must have at least one violation");
    }

    [Fact]
    public void Create_WithNegativeDuration_ShouldFail()
    {
        // Act
        var result = PolicyDecision.Create(
            allowed: true,
            evaluationDurationMs: -100);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be negative");
    }

    [Fact]
    public void GetReason_WhenAllowed_ShouldReturnSuccessMessage()
    {
        // Arrange
        var decision = PolicyDecision.Create(allowed: true).Value;

        // Act
        var reason = decision.GetReason();

        // Assert
        reason.Should().Be("All compliance checks passed");
    }

    [Fact]
    public void GetReason_WhenDeniedWithSingleViolation_ShouldReturnViolation()
    {
        // Arrange
        var violations = new List<string> { "Critical vulnerabilities detected" };
        var decision = PolicyDecision.Create(allowed: false, violations: violations).Value;

        // Act
        var reason = decision.GetReason();

        // Assert
        reason.Should().Be("Critical vulnerabilities detected");
    }

    [Fact]
    public void GetReason_WhenDeniedWithMultipleViolations_ShouldReturnCount()
    {
        // Arrange
        var violations = new List<string>
        {
            "Critical vulnerabilities detected",
            "Missing required scans",
            "Policy compliance failed"
        };
        var decision = PolicyDecision.Create(allowed: false, violations: violations).Value;

        // Act
        var reason = decision.GetReason();

        // Assert
        reason.Should().Be("3 policy violations found");
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var violations = new List<string> { "Violation" };
        var decision1 = PolicyDecision.Create(allowed: false, violations: violations, evaluationDurationMs: 150).Value;
        var decision2 = PolicyDecision.Create(allowed: false, violations: violations, evaluationDurationMs: 150).Value;

        // Assert
        decision1.Should().Be(decision2);
    }

    [Fact]
    public void Equality_WithDifferentAllowed_ShouldNotBeEqual()
    {
        // Arrange
        var violations = new List<string> { "Violation" };
        var decision1 = PolicyDecision.Create(allowed: true).Value;
        var decision2 = PolicyDecision.Create(allowed: false, violations: violations).Value;

        // Assert
        decision1.Should().NotBe(decision2);
    }
}
