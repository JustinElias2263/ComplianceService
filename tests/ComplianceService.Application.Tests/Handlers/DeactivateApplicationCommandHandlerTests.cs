using ComplianceService.Application.Commands;
using ComplianceService.Application.Handlers.Commands;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using FluentAssertions;
using Moq;
using Xunit;
using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;

namespace ComplianceService.Application.Tests.Handlers;

public class DeactivateApplicationCommandHandlerTests
{
    private readonly Mock<IApplicationRepository> _mockRepository;
    private readonly DeactivateApplicationCommandHandler _handler;

    public DeactivateApplicationCommandHandlerTests()
    {
        _mockRepository = new Mock<IApplicationRepository>();
        _handler = new DeactivateApplicationCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldDeactivateApplication()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;
        var command = new DeactivateApplicationCommand
        {
            ApplicationId = application.Id
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
        application.IsActive.Should().BeFalse();

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenApplicationNotFound_ShouldFail()
    {
        // Arrange
        var command = new DeactivateApplicationCommand
        {
            ApplicationId = Guid.NewGuid()
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
    public async Task Handle_WhenAlreadyDeactivated_ShouldSucceed()
    {
        // Arrange
        var application = DomainApplication.Create("TestApp", "team@example.com").Value;
        application.Deactivate();

        var command = new DeactivateApplicationCommand
        {
            ApplicationId = application.Id
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
        application.IsActive.Should().BeFalse();
    }
}
