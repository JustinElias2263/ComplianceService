using ComplianceService.Application.Handlers.Queries;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].Name.Should().Be("App1");
        result.Value[1].Name.Should().Be("App2");
        result.Value[2].Name.Should().Be("App3");

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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();

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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(10);
        result.Value[0].Name.Should().Be("App11");
        result.Value[9].Name.Should().Be("App20");

        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithOwnerFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var app1 = DomainApplication.Create("App1", "team1@example.com").Value;
        var app2 = DomainApplication.Create("App2", "team2@example.com").Value;
        var app3 = DomainApplication.Create("App3", "team1@example.com").Value;

        var applications = new List<DomainApplication> { app1, app2, app3 };

        var query = new GetAllApplicationsQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Owner = "team1@example.com"
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(applications);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Name.Should().Be("App1");
        result.Value[1].Name.Should().Be("App3");

        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
