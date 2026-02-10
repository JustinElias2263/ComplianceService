using ComplianceService.Domain.Evaluation.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.ValueObjects;

public class ScanResultTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var scanDate = DateTime.UtcNow.AddHours(-1);
        var vulnerabilities = new List<Vulnerability>();

        // Act
        var result = ScanResult.Create(
            tool: "snyk",
            toolVersion: "1.1000.0",
            scanDate: scanDate,
            vulnerabilities: vulnerabilities,
            projectId: "proj-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tool.Should().Be("snyk");
        result.Value.ToolVersion.Should().Be("1.1000.0");
        result.Value.ScanDate.Should().Be(scanDate);
        result.Value.ProjectId.Should().Be("proj-123");
        result.Value.Vulnerabilities.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithoutProjectId_ShouldSucceed()
    {
        // Arrange
        var scanDate = DateTime.UtcNow.AddHours(-1);
        var vulnerabilities = new List<Vulnerability>();

        // Act
        var result = ScanResult.Create(
            tool: "snyk",
            toolVersion: "1.1000.0",
            scanDate: scanDate,
            vulnerabilities: vulnerabilities);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldNormalizeToolName()
    {
        // Arrange
        var scanDate = DateTime.UtcNow.AddHours(-1);

        // Act
        var result = ScanResult.Create(
            tool: "  SNYK  ",
            toolVersion: "1.1000.0",
            scanDate: scanDate,
            vulnerabilities: new List<Vulnerability>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tool.Should().Be("snyk");
    }

    [Fact]
    public void Create_WithNullVulnerabilities_ShouldUseEmptyList()
    {
        // Arrange
        var scanDate = DateTime.UtcNow.AddHours(-1);

        // Act
        var result = ScanResult.Create(
            tool: "snyk",
            toolVersion: "1.1000.0",
            scanDate: scanDate,
            vulnerabilities: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Vulnerabilities.Should().NotBeNull();
        result.Value.Vulnerabilities.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidTool_ShouldFail(string invalidTool)
    {
        // Act
        var result = ScanResult.Create(
            tool: invalidTool,
            toolVersion: "1.1000.0",
            scanDate: DateTime.UtcNow,
            vulnerabilities: new List<Vulnerability>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Tool name cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidToolVersion_ShouldFail(string invalidVersion)
    {
        // Act
        var result = ScanResult.Create(
            tool: "snyk",
            toolVersion: invalidVersion,
            scanDate: DateTime.UtcNow,
            vulnerabilities: new List<Vulnerability>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Tool version cannot be empty");
    }

    [Fact]
    public void Create_WithFutureScanDate_ShouldFail()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(2);

        // Act
        var result = ScanResult.Create(
            tool: "snyk",
            toolVersion: "1.1000.0",
            scanDate: futureDate,
            vulnerabilities: new List<Vulnerability>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be in the future");
    }

    [Fact]
    public void VulnerabilityCounts_ShouldBeCorrect()
    {
        // Arrange
        var vulnerabilities = new List<Vulnerability>
        {
            Vulnerability.Create("V1", "Critical Issue", "critical", 9.8, "pkg1", "1.0.0").Value,
            Vulnerability.Create("V2", "Critical Issue 2", "critical", 9.5, "pkg2", "1.0.0").Value,
            Vulnerability.Create("V3", "High Issue", "high", 7.5, "pkg3", "1.0.0").Value,
            Vulnerability.Create("V4", "High Issue 2", "high", 7.0, "pkg4", "1.0.0").Value,
            Vulnerability.Create("V5", "High Issue 3", "high", 8.0, "pkg5", "1.0.0").Value,
            Vulnerability.Create("V6", "Medium Issue", "medium", 5.0, "pkg6", "1.0.0").Value,
            Vulnerability.Create("V7", "Medium Issue 2", "medium", 4.5, "pkg7", "1.0.0").Value,
            Vulnerability.Create("V8", "Low Issue", "low", 2.0, "pkg8", "1.0.0").Value
        };

        var scanResult = ScanResult.Create(
            "snyk", "1.1000.0", DateTime.UtcNow.AddHours(-1), vulnerabilities).Value;

        // Assert
        scanResult.CriticalCount.Should().Be(2);
        scanResult.HighCount.Should().Be(3);
        scanResult.MediumCount.Should().Be(2);
        scanResult.LowCount.Should().Be(1);
        scanResult.TotalCount.Should().Be(8);
    }

    [Fact]
    public void VulnerabilityCounts_WithNoVulnerabilities_ShouldBeZero()
    {
        // Arrange
        var scanResult = ScanResult.Create(
            "snyk", "1.1000.0", DateTime.UtcNow.AddHours(-1), new List<Vulnerability>()).Value;

        // Assert
        scanResult.CriticalCount.Should().Be(0);
        scanResult.HighCount.Should().Be(0);
        scanResult.MediumCount.Should().Be(0);
        scanResult.LowCount.Should().Be(0);
        scanResult.TotalCount.Should().Be(0);
    }

    [Fact]
    public void Equality_WithSameToolAndDate_ShouldBeEqual()
    {
        // Arrange
        var scanDate = DateTime.UtcNow.AddHours(-1);
        var scan1 = ScanResult.Create("snyk", "1.1000.0", scanDate, new List<Vulnerability>(), "proj-123").Value;
        var scan2 = ScanResult.Create("snyk", "1.1000.0", scanDate, new List<Vulnerability>(), "proj-123").Value;

        // Assert
        scan1.Should().Be(scan2);
    }

    [Fact]
    public void Equality_WithDifferentTool_ShouldNotBeEqual()
    {
        // Arrange
        var scanDate = DateTime.UtcNow.AddHours(-1);
        var scan1 = ScanResult.Create("snyk", "1.1000.0", scanDate, new List<Vulnerability>()).Value;
        var scan2 = ScanResult.Create("prismacloud", "1.0.0", scanDate, new List<Vulnerability>()).Value;

        // Assert
        scan1.Should().NotBe(scan2);
    }
}
