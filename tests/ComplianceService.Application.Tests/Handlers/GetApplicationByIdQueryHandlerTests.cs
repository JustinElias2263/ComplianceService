using ComplianceService.Application.Handlers.Queries;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Entities;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using ComplianceService.Domain.Shared;
using FluentAssertions;
using Moq;
using Xunit;
using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;

namespace ComplianceService.Application.Tests.Handlers;

public class GetApplicationByIdQueryHandlerTests
{
    private readonly Mock<IApplicationRepository> _mockRepository;
    private readonly GetApplicationByIdQueryHandler _handler;

    public GetApplicationByIdQueryHandlerTests()
    {
        _mockRepository = new Mock<IApplicationRepository>();
        _handler = new GetApplicationByIdQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingApplication_ShouldReturnDto()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;
        var riskTier = RiskTier.Create("critical").Value;
        var tools = new List<SecurityToolType> { SecurityToolType.Snyk };
        var policies = new List<PolicyReference> { PolicyReference.Create("compliance/production").Value };

        var environmentConfig = EnvironmentConfig.Create(
            application.Id,
            "production",
            riskTier,
            tools,
            policies).Value;
        application.AddEnvironment(environmentConfig);

        var query = new GetApplicationByIdQuery { ApplicationId = application.Id };

        _mockRepository
            .Setup(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(application.Id);
        result.Value.Name.Should().Be("TestApp");
        result.Value.Owner.Should().Be("team@example.com");
        result.Value.Environments.Should().HaveCount(1);
        result.Value.Environments[0].Name.Should().Be("production");

        _mockRepository.Verify(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingApplication_ShouldReturnFailure()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var query = new GetApplicationByIdQuery { ApplicationId = applicationId };

        _mockRepository
            .Setup(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainApplication>("Application not found"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");

        _mockRepository.Verify(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithApplicationWithoutEnvironments_ShouldReturnEmptyEnvironmentsList()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;
        var query = new GetApplicationByIdQuery { ApplicationId = application.Id };

        _mockRepository
            .Setup(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Environments.Should().BeEmpty();
    }
}
