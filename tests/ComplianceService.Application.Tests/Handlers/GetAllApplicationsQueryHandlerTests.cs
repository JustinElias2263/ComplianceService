using ComplianceService.Application.Handlers.Queries;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;
using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;

namespace ComplianceService.Application.Tests.Handlers;

public class GetAllApplicationsQueryHandlerTests
{
    private readonly Mock<IApplicationRepository> _mockRepository;
    private readonly GetAllApplicationsQueryHandler _handler;

    public GetAllApplicationsQueryHandlerTests()
    {
        _mockRepository = new Mock<IApplicationRepository>();
        _handler = new GetAllApplicationsQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithMultipleApplications_ShouldReturnAll()
    {
        // Arrange
        var app1 = DomainApplication.Create("App1", "team1@example.com").Value;
        var app2 = DomainApplication.Create("App2", "team2@example.com").Value;
        var app3 = DomainApplication.Create("App3", "team3@example.com").Value;

        var applications = new List<DomainApplication> { app1, app2, app3 };

        var query = new GetAllApplicationsQuery
        {
            PageNumber = 1,
            PageSize = 10
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(applications);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("App1");
        result[1].Name.Should().Be("App2");
        result[2].Name.Should().Be("App3");

        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoApplications_ShouldReturnEmptyList()
    {
        // Arrange
        var query = new GetAllApplicationsQuery
        {
            PageNumber = 1,
            PageSize = 10
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DomainApplication>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();

        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var applications = Enumerable.Range(1, 25)
            .Select(i => DomainApplication.Create($"App{i}", $"team{i}@example.com").Value)
            .ToList();

        var query = new GetAllApplicationsQuery
        {
            PageNumber = 2,
            PageSize = 10
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(applications);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(10);
        result[0].Name.Should().Be("App11");
        result[9].Name.Should().Be("App20");

        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithFilterByRiskTier_ShouldReturnFilteredResults()
    {
        // Arrange
        var app1 = DomainApplication.Create("App1", "team1@example.com").Value;
        var app2 = DomainApplication.Create("App2", "team2@example.com").Value;

        var riskTierCritical = RiskTier.Create("critical").Value;
        var riskTierHigh = RiskTier.Create("high").Value;
        var tools = new List<SecurityToolType> { SecurityToolType.Snyk };
        var policies = new List<PolicyReference> { PolicyReference.Create("compliance/prod").Value };

        app1.AddEnvironment("production", riskTierCritical, tools, policies);
        app2.AddEnvironment("production", riskTierHigh, tools, policies);

        var criticalApps = new List<DomainApplication> { app1 };

        var query = new GetAllApplicationsQuery
        {
            PageNumber = 1,
            PageSize = 10,
            RiskTier = "critical"
        };

        _mockRepository
            .Setup(r => r.GetByRiskTierAsync("critical", It.IsAny<CancellationToken>()))
            .ReturnsAsync(criticalApps);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("App1");

        _mockRepository.Verify(r => r.GetByRiskTierAsync("critical", It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
