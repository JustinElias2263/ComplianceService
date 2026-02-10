using ComplianceService.Application.Handlers.Queries;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using FluentAssertions;
using Moq;
using Xunit;
using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;

namespace ComplianceService.Application.Tests.Handlers;

public class GetApplicationByNameQueryHandlerTests
{
    private readonly Mock<IApplicationRepository> _mockRepository;
    private readonly GetApplicationByNameQueryHandler _handler;

    public GetApplicationByNameQueryHandlerTests()
    {
        _mockRepository = new Mock<IApplicationRepository>();
        _handler = new GetApplicationByNameQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingApplicationName_ShouldReturnDto()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;
        var query = new GetApplicationByNameQuery { Name = "TestApp" };

        _mockRepository
            .Setup(r => r.GetByNameAsync("TestApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("TestApp");
        result.Value.Owner.Should().Be("team@example.com");

        _mockRepository.Verify(r => r.GetByNameAsync("TestApp", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingApplicationName_ShouldReturnFailure()
    {
        // Arrange
        var query = new GetApplicationByNameQuery { Name = "NonExistent" };

        _mockRepository
            .Setup(r => r.GetByNameAsync("NonExistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainApplication>("Application not found"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");

        _mockRepository.Verify(r => r.GetByNameAsync("NonExistent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDifferentCasing_ShouldQueryExactName()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;
        var query = new GetApplicationByNameQuery { Name = "TESTAPP" };

        _mockRepository
            .Setup(r => r.GetByNameAsync("TESTAPP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockRepository.Verify(r => r.GetByNameAsync("TESTAPP", It.IsAny<CancellationToken>()), Times.Once);
    }
}
