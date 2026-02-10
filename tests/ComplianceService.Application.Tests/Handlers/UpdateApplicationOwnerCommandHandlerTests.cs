using ComplianceService.Application.Commands;
using ComplianceService.Application.Handlers.Commands;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using FluentAssertions;
using Moq;
using Xunit;
using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;

namespace ComplianceService.Application.Tests.Handlers;

public class UpdateApplicationOwnerCommandHandlerTests
{
    private readonly Mock<IApplicationRepository> _mockRepository;
    private readonly UpdateApplicationOwnerCommandHandler _handler;

    public UpdateApplicationOwnerCommandHandlerTests()
    {
        _mockRepository = new Mock<IApplicationRepository>();
        _handler = new UpdateApplicationOwnerCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldUpdateOwner()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "old-team@example.com").Value;
        var command = new UpdateApplicationOwnerCommand
        {
            ApplicationId = application.Id,
            NewOwner = "new-team@example.com"
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>()))
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
        result.Value.Owner.Should().Be("new-team@example.com");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenApplicationNotFound_ShouldFail()
    {
        // Arrange
        var command = new UpdateApplicationOwnerCommand
        {
            ApplicationId = Guid.NewGuid(),
            NewOwner = "new-team@example.com"
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

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Handle_WithInvalidOwner_ShouldFail(string invalidOwner)
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "old-team@example.com").Value;
        var command = new UpdateApplicationOwnerCommand
        {
            ApplicationId = application.Id,
            NewOwner = invalidOwner
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(application));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().ContainAny("owner", "Owner", "empty");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
