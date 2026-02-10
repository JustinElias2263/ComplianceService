using ComplianceService.Application.Commands;
using ComplianceService.Application.Handlers.Commands;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using FluentAssertions;
using Moq;
using Xunit;
using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;

namespace ComplianceService.Application.Tests.Handlers;

public class RegisterApplicationCommandHandlerTests
{
    private readonly Mock<IApplicationRepository> _mockRepository;
    private readonly RegisterApplicationCommandHandler _handler;

    public RegisterApplicationCommandHandlerTests()
    {
        _mockRepository = new Mock<IApplicationRepository>();
        _handler = new RegisterApplicationCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldRegisterApplication()
    {
        // Arrange
        var command = new RegisterApplicationCommand
        {
            Name = "TestApp",
            Owner = "team@example.com"
        };

        _mockRepository
            .Setup(r => r.GetByNameAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainApplication>("Not found"));

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mockRepository
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("TestApp");
        result.Value.Owner.Should().Be("team@example.com");
        result.Value.Environments.Should().BeEmpty();

        _mockRepository.Verify(r => r.GetByNameAsync(command.Name, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ShouldFail()
    {
        // Arrange
        var command = new RegisterApplicationCommand
        {
            Name = "ExistingApp",
            Owner = "team@example.com"
        };

        var existingApp = DomainApplication.Create("ExistingApp", "existing@example.com").Value;

        _mockRepository
            .Setup(r => r.GetByNameAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(existingApp));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");

        _mockRepository.Verify(r => r.GetByNameAsync(command.Name, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Handle_WithInvalidName_ShouldFail(string invalidName)
    {
        // Arrange
        var command = new RegisterApplicationCommand
        {
            Name = invalidName,
            Owner = "team@example.com"
        };

        _mockRepository
            .Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainApplication>("Not found"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Handle_WithInvalidOwner_ShouldFail(string invalidOwner)
    {
        // Arrange
        var command = new RegisterApplicationCommand
        {
            Name = "TestApp",
            Owner = invalidOwner
        };

        _mockRepository
            .Setup(r => r.GetByNameAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainApplication>("Not found"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner");

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryAddFails_ShouldPropagateError()
    {
        // Arrange
        var command = new RegisterApplicationCommand
        {
            Name = "TestApp",
            Owner = "team@example.com"
        };

        _mockRepository
            .Setup(r => r.GetByNameAsync(command.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainApplication>("Not found"));

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<DomainApplication>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _handler.Handle(command, CancellationToken.None);
        });
    }
}
