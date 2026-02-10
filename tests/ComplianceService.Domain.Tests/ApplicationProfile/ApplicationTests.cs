using ComplianceService.Domain.ApplicationProfile;
using ComplianceService.Domain.ApplicationProfile.Entities;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Domain.Tests.ApplicationProfile;

public class ApplicationTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var name = "MyService";
        var owner = "team@example.com";

        // Act
        var result = Application.Create(name, owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(name);
        result.Value.Owner.Should().Be(owner);
        result.Value.IsActive.Should().BeTrue();
        result.Value.Environments.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ShouldFail(string invalidName)
    {
        // Arrange
        var owner = "team@example.com";

        // Act
        var result = Application.Create(invalidName, owner);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");
    }

    [Theory]
    [InlineData("ab")]  // Too short
    public void Create_WithNameTooShort_ShouldFail(string shortName)
    {
        // Arrange
        var owner = "team@example.com";

        // Act
        var result = Application.Create(shortName, owner);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between 3 and 100 characters");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("missing@domain")]
    [InlineData("")]
    public void Create_WithInvalidOwner_ShouldFail(string invalidOwner)
    {
        // Arrange
        var name = "MyService";

        // Act
        var result = Application.Create(name, invalidOwner);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner");
    }

    [Fact]
    public void AddEnvironment_WithValidConfig_ShouldSucceed()
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;
        var environmentConfig = EnvironmentConfig.Create(
            application.Id,
            "production",
            RiskTier.Critical,
            new List<SecurityToolType> { SecurityToolType.Snyk },
            new List<PolicyReference> { PolicyReference.Create("compliance.cicd.production").Value }
        ).Value;

        // Act
        var result = application.AddEnvironment(environmentConfig);

        // Assert
        result.IsSuccess.Should().BeTrue();
        application.Environments.Should().HaveCount(1);
        application.Environments.First().EnvironmentName.Should().Be("production");
    }

    [Fact]
    public void AddEnvironment_WithDuplicateName_ShouldFail()
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;
        var env1 = EnvironmentConfig.Create(
            application.Id,
            "production",
            RiskTier.Critical,
            new List<SecurityToolType> { SecurityToolType.Snyk },
            new List<PolicyReference> { PolicyReference.Create("compliance.cicd.production").Value }
        ).Value;
        var env2 = EnvironmentConfig.Create(
            application.Id,
            "production",
            RiskTier.High,
            new List<SecurityToolType> { SecurityToolType.PrismaCloud },
            new List<PolicyReference> { PolicyReference.Create("compliance.cicd.production").Value }
        ).Value;

        application.AddEnvironment(env1);

        // Act
        var result = application.AddEnvironment(env2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public void GetEnvironment_WhenExists_ShouldReturnEnvironment()
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;
        var environmentConfig = EnvironmentConfig.Create(
            application.Id,
            "production",
            RiskTier.Critical,
            new List<SecurityToolType> { SecurityToolType.Snyk },
            new List<PolicyReference> { PolicyReference.Create("compliance.cicd.production").Value }
        ).Value;
        application.AddEnvironment(environmentConfig);

        // Act
        var result = application.GetEnvironment("production");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EnvironmentName.Should().Be("production");
        result.Value.RiskTier.Should().Be(RiskTier.Critical);
    }

    [Fact]
    public void GetEnvironment_WhenNotExists_ShouldFail()
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;

        // Act
        var result = application.GetEnvironment("production");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void UpdateOwner_WithValidEmail_ShouldSucceed()
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;
        var newOwner = "newteam@example.com";

        // Act
        var result = application.UpdateOwner(newOwner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        application.Owner.Should().Be(newOwner);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("")]
    public void UpdateOwner_WithInvalidEmail_ShouldFail(string invalidEmail)
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;

        // Act
        var result = application.UpdateOwner(invalidEmail);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("valid email");
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;

        // Act
        application.Deactivate();

        // Assert
        application.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RemoveEnvironment_WhenExists_ShouldSucceed()
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;
        var environmentConfig = EnvironmentConfig.Create(
            application.Id,
            "production",
            RiskTier.Critical,
            new List<SecurityToolType> { SecurityToolType.Snyk },
            new List<PolicyReference> { PolicyReference.Create("compliance.cicd.production").Value }
        ).Value;
        application.AddEnvironment(environmentConfig);

        // Act
        var result = application.RemoveEnvironment("production");

        // Assert
        result.IsSuccess.Should().BeTrue();
        application.Environments.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEnvironment_WhenNotExists_ShouldFail()
    {
        // Arrange
        var application = Application.Create("MyService", "team@example.com").Value;

        // Act
        var result = application.RemoveEnvironment("production");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }
}
