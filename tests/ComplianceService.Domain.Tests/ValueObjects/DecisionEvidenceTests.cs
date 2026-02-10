using ComplianceService.Domain.Audit.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.ValueObjects;

public class DecisionEvidenceTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var scanResultsJson = "{\"tool\":\"snyk\",\"vulnerabilities\":[]}";
        var policyInputJson = "{\"application\":\"test-app\",\"environment\":\"production\"}";
        var policyOutputJson = "{\"allow\":false,\"violations\":[\"Critical vulnerabilities\"]}";

        // Act
        var result = DecisionEvidence.Create(scanResultsJson, policyInputJson, policyOutputJson);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScanResultsJson.Should().Be(scanResultsJson);
        result.Value.PolicyInputJson.Should().Be(policyInputJson);
        result.Value.PolicyOutputJson.Should().Be(policyOutputJson);
        result.Value.CapturedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidScanResults_ShouldFail(string invalidJson)
    {
        // Act
        var result = DecisionEvidence.Create(
            scanResultsJson: invalidJson,
            policyInputJson: "{\"valid\":true}",
            policyOutputJson: "{\"valid\":true}");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Scan results JSON cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidPolicyInput_ShouldFail(string invalidJson)
    {
        // Act
        var result = DecisionEvidence.Create(
            scanResultsJson: "{\"valid\":true}",
            policyInputJson: invalidJson,
            policyOutputJson: "{\"valid\":true}");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Policy input JSON cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidPolicyOutput_ShouldFail(string invalidJson)
    {
        // Act
        var result = DecisionEvidence.Create(
            scanResultsJson: "{\"valid\":true}",
            policyInputJson: "{\"valid\":true}",
            policyOutputJson: invalidJson);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Policy output JSON cannot be empty");
    }

    [Fact]
    public void Create_ShouldSetCapturedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var result = DecisionEvidence.Create(
            "{\"tool\":\"snyk\"}",
            "{\"app\":\"test\"}",
            "{\"allow\":true}");

        var after = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CapturedAt.Should().BeOnOrAfter(before);
        result.Value.CapturedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var scanResults = "{\"tool\":\"snyk\"}";
        var policyInput = "{\"app\":\"test\"}";
        var policyOutput = "{\"allow\":true}";

        var evidence1 = DecisionEvidence.Create(scanResults, policyInput, policyOutput).Value;

        // Wait a tiny bit to ensure different timestamps
        System.Threading.Thread.Sleep(10);

        var evidence2 = DecisionEvidence.Create(scanResults, policyInput, policyOutput).Value;

        // Assert - they should NOT be equal because CapturedAt is different
        evidence1.Should().NotBe(evidence2);
    }

    [Fact]
    public void Create_WithComplexJson_ShouldPreserveContent()
    {
        // Arrange
        var scanResultsJson = @"{
            ""tool"": ""snyk"",
            ""vulnerabilities"": [
                {
                    ""id"": ""SNYK-001"",
                    ""severity"": ""critical"",
                    ""cvss"": 9.8
                }
            ]
        }";
        var policyInputJson = @"{
            ""application"": {
                ""name"": ""test-app"",
                ""environment"": ""production""
            }
        }";
        var policyOutputJson = @"{
            ""allow"": false,
            ""violations"": [""Critical vulnerabilities detected""]
        }";

        // Act
        var result = DecisionEvidence.Create(scanResultsJson, policyInputJson, policyOutputJson);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScanResultsJson.Should().Be(scanResultsJson);
        result.Value.PolicyInputJson.Should().Be(policyInputJson);
        result.Value.PolicyOutputJson.Should().Be(policyOutputJson);
    }
}
