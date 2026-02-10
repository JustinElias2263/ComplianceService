using ComplianceService.Application.Commands;
using ComplianceService.Application.Handlers.Commands;
using ComplianceService.Domain.ApplicationProfile.Entities;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using ComplianceService.Domain.Shared;
using FluentAssertions;
using Moq;
using Xunit;
using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;

namespace ComplianceService.Application.Tests.Handlers;

public class AddEnvironmentConfigCommandHandlerTests
{
    private readonly Mock<IApplicationRepository> _mockRepository;
    private readonly AddEnvironmentConfigCommandHandler _handler;

    public AddEnvironmentConfigCommandHandlerTests()
    {
        _mockRepository = new Mock<IApplicationRepository>();
        _handler = new AddEnvironmentConfigCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldAddEnvironment()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;
        var applicationId = application.Id;

        var command = new AddEnvironmentConfigCommand
        {
            ApplicationId = applicationId,
            EnvironmentName = "production",
            RiskTier = "critical",
            SecurityTools = new List<string> { "snyk", "prismacloud" },
            PolicyReferences = new List<string> { "compliance/cicd/production" }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mockRepository
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("TestApp");
        result.Value.Environments.Should().HaveCount(1);
        result.Value.Environments[0].Name.Should().Be("production");
        result.Value.Environments[0].RiskTier.Should().Be("critical");
        result.Value.Environments[0].SecurityTools.Should().Contain("snyk");
        result.Value.Environments[0].PolicyReferences.Should().Contain("compliance/cicd/production");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenApplicationNotFound_ShouldFail()
    {
        // Arrange
        var command = new AddEnvironmentConfigCommand
        {
            ApplicationId = Guid.NewGuid(),
            EnvironmentName = "production",
            RiskTier = "critical",
            SecurityTools = new List<string> { "snyk" },
            PolicyReferences = new List<string> { "compliance/cicd/production" }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(command.ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainApplication>("Application not found"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithDuplicateEnvironment_ShouldFail()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;
        var riskTier = RiskTier.Create("high").Value;
        var tools = new List<SecurityToolType> { SecurityToolType.Snyk };
        var policies = new List<PolicyReference> { PolicyReference.Create("compliance/staging").Value };

        var environmentConfig = EnvironmentConfig.Create(
            application.Id,
            "staging",
            riskTier,
            tools,
            policies).Value;
        application.AddEnvironment(environmentConfig);

        var command = new AddEnvironmentConfigCommand
        {
            ApplicationId = application.Id,
            EnvironmentName = "staging",
            RiskTier = "high",
            SecurityTools = new List<string> { "snyk" },
            PolicyReferences = new List<string> { "compliance/staging" }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(command.ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("invalid-tier")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_WithInvalidRiskTier_ShouldFail(string invalidTier)
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;

        var command = new AddEnvironmentConfigCommand
        {
            ApplicationId = application.Id,
            EnvironmentName = "production",
            RiskTier = invalidTier,
            SecurityTools = new List<string> { "snyk" },
            PolicyReferences = new List<string> { "compliance/production" }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(command.ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidSecurityTool_ShouldFail()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;

        var command = new AddEnvironmentConfigCommand
        {
            ApplicationId = application.Id,
            EnvironmentName = "production",
            RiskTier = "critical",
            SecurityTools = new List<string> { "invalid-tool" },
            PolicyReferences = new List<string> { "compliance/production" }
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(command.ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unsupported security tool");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
