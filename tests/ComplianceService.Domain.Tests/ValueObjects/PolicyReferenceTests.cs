using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.ValueObjects;

public class PolicyReferenceTests
{
    [Theory]
    [InlineData("compliance/production")]
    [InlineData("compliance.cicd.production")]
    [InlineData("security/critical-checks")]
    [InlineData("org.policies.production")]
    public void Create_WithValidFormat_ShouldSucceed(string packageName)
    {
        // Act
        var result = PolicyReference.Create(packageName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PackageName.Should().Be(packageName);
    }

    [Fact]
    public void Create_WithWhitespace_ShouldTrim()
    {
        // Act
        var result = PolicyReference.Create("  compliance/production  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PackageName.Should().Be("compliance/production");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithEmptyValue_ShouldFail(string invalidPackage)
    {
        // Act
        var result = PolicyReference.Create(invalidPackage);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Theory]
    [InlineData("nodelimiter")]
    [InlineData("production")]
    public void Create_WithoutSeparator_ShouldFail(string invalidPackage)
    {
        // Act
        var result = PolicyReference.Create(invalidPackage);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("package separator");
    }

    [Fact]
    public void Create_WithTooShortName_ShouldFail()
    {
        // Arrange
        var shortName = "a/";  // Only 2 characters

        // Act
        var result = PolicyReference.Create(shortName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between 3 and 200");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldFail()
    {
        // Arrange
        var longName = "compliance/" + new string('x', 200);

        // Act
        var result = PolicyReference.Create(longName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between 3 and 200");
    }

    [Fact]
    public void Equality_WithSamePackageName_ShouldBeEqual()
    {
        // Arrange
        var policy1 = PolicyReference.Create("compliance/production").Value;
        var policy2 = PolicyReference.Create("compliance/production").Value;

        // Assert
        policy1.Should().Be(policy2);
        (policy1 == policy2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentPackageName_ShouldNotBeEqual()
    {
        // Arrange
        var policy1 = PolicyReference.Create("compliance/production").Value;
        var policy2 = PolicyReference.Create("compliance/staging").Value;

        // Assert
        policy1.Should().NotBe(policy2);
        (policy1 != policy2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldReturnPackageName()
    {
        // Arrange
        var policy = PolicyReference.Create("compliance/production").Value;

        // Act
        var result = policy.ToString();

        // Assert
        result.Should().Be("compliance/production");
    }
}
