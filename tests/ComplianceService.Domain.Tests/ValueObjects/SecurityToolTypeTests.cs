using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.ValueObjects;

public class SecurityToolTypeTests
{
    [Theory]
    [InlineData("snyk")]
    [InlineData("prismacloud")]
    public void Create_WithValidTool_ShouldSucceed(string toolName)
    {
        // Act
        var result = SecurityToolType.Create(toolName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(toolName.ToLowerInvariant());
    }

    [Theory]
    [InlineData("SNYK")]
    [InlineData("PrismaCloud")]
    [InlineData("  snyk  ")]
    public void Create_WithDifferentCasing_ShouldNormalize(string toolName)
    {
        // Act
        var result = SecurityToolType.Create(toolName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(toolName.Trim().ToLowerInvariant());
    }

    [Fact]
    public void Create_WithPrismaShorthand_ShouldReturnPrismaCloud()
    {
        // Act
        var result = SecurityToolType.Create("prisma");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(SecurityToolType.PrismaCloud);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithEmptyValue_ShouldFail(string invalidTool)
    {
        // Act
        var result = SecurityToolType.Create(invalidTool);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Theory]
    [InlineData("invalid-tool")]
    [InlineData("fortify")]
    [InlineData("checkmarx")]
    public void Create_WithUnsupportedTool_ShouldFail(string invalidTool)
    {
        // Act
        var result = SecurityToolType.Create(invalidTool);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unsupported security tool");
    }

    [Fact]
    public void PredefinedValues_ShouldBeAccessible()
    {
        // Assert
        SecurityToolType.Snyk.Name.Should().Be("snyk");
        SecurityToolType.PrismaCloud.Name.Should().Be("prismacloud");
    }

    [Fact]
    public void Equality_WithSameValue_ShouldBeEqual()
    {
        // Arrange
        var tool1 = SecurityToolType.Create("snyk").Value;
        var tool2 = SecurityToolType.Create("snyk").Value;

        // Assert
        tool1.Should().Be(tool2);
        (tool1 == tool2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentValue_ShouldNotBeEqual()
    {
        // Arrange
        var tool1 = SecurityToolType.Create("snyk").Value;
        var tool2 = SecurityToolType.Create("prismacloud").Value;

        // Assert
        tool1.Should().NotBe(tool2);
        (tool1 != tool2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldReturnName()
    {
        // Arrange
        var tool = SecurityToolType.Create("snyk").Value;

        // Act
        var result = tool.ToString();

        // Assert
        result.Should().Be("snyk");
    }
}
