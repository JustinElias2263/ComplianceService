using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.ValueObjects;

public class RiskTierTests
{
    [Theory]
    [InlineData("critical")]
    [InlineData("high")]
    [InlineData("medium")]
    [InlineData("low")]
    public void Create_WithValidRiskTier_ShouldSucceed(string tier)
    {
        // Act
        var result = RiskTier.Create(tier);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(tier.ToLowerInvariant());
    }

    [Theory]
    [InlineData("CRITICAL")]
    [InlineData("High")]
    [InlineData("  medium  ")]
    public void Create_WithDifferentCasing_ShouldNormalize(string tier)
    {
        // Act
        var result = RiskTier.Create(tier);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(tier.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithEmptyValue_ShouldFail(string invalidTier)
    {
        // Act
        var result = RiskTier.Create(invalidTier);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("super-high")]
    [InlineData("123")]
    public void Create_WithInvalidTier_ShouldFail(string invalidTier)
    {
        // Act
        var result = RiskTier.Create(invalidTier);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid risk tier");
    }

    [Fact]
    public void PredefinedValues_ShouldBeAccessible()
    {
        // Assert
        RiskTier.Critical.Value.Should().Be("critical");
        RiskTier.High.Value.Should().Be("high");
        RiskTier.Medium.Value.Should().Be("medium");
        RiskTier.Low.Value.Should().Be("low");
    }

    [Fact]
    public void IsCritical_WhenCritical_ShouldBeTrue()
    {
        // Arrange
        var tier = RiskTier.Create("critical").Value;

        // Assert
        tier.IsCritical.Should().BeTrue();
    }

    [Theory]
    [InlineData("high")]
    [InlineData("medium")]
    [InlineData("low")]
    public void IsCritical_WhenNotCritical_ShouldBeFalse(string tierValue)
    {
        // Arrange
        var tier = RiskTier.Create(tierValue).Value;

        // Assert
        tier.IsCritical.Should().BeFalse();
    }

    [Theory]
    [InlineData("critical")]
    [InlineData("high")]
    public void IsHighOrAbove_WhenHighOrCritical_ShouldBeTrue(string tierValue)
    {
        // Arrange
        var tier = RiskTier.Create(tierValue).Value;

        // Assert
        tier.IsHighOrAbove.Should().BeTrue();
    }

    [Theory]
    [InlineData("medium")]
    [InlineData("low")]
    public void IsHighOrAbove_WhenBelowHigh_ShouldBeFalse(string tierValue)
    {
        // Arrange
        var tier = RiskTier.Create(tierValue).Value;

        // Assert
        tier.IsHighOrAbove.Should().BeFalse();
    }

    [Fact]
    public void Equality_WithSameValue_ShouldBeEqual()
    {
        // Arrange
        var tier1 = RiskTier.Create("critical").Value;
        var tier2 = RiskTier.Create("critical").Value;

        // Assert
        tier1.Should().Be(tier2);
        (tier1 == tier2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentValue_ShouldNotBeEqual()
    {
        // Arrange
        var tier1 = RiskTier.Create("critical").Value;
        var tier2 = RiskTier.Create("high").Value;

        // Assert
        tier1.Should().NotBe(tier2);
        (tier1 != tier2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        // Arrange
        var tier = RiskTier.Create("critical").Value;

        // Act
        var result = tier.ToString();

        // Assert
        result.Should().Be("critical");
    }
}
