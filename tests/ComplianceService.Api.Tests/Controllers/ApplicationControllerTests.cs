using System.Net;
using System.Net.Http.Json;
using ComplianceService.Api.Tests.Fixtures;
using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Api.Tests.Controllers;

/// <summary>
/// Integration tests for ApplicationController
/// Tests the full HTTP request/response flow with realistic scenarios
/// </summary>
public class ApplicationControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ApplicationControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterApplication_WithValidData_ShouldReturn201Created()
    {
        // Arrange
        var command = TestDataGenerator.CreateProductionApplication("E-Commerce-Service");

        // Act
        var response = await _client.PostAsJsonAsync("/api/applications", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var application = await response.Content.ReadFromJsonAsync<ApplicationDto>();
        application.Should().NotBeNull();
        application!.Name.Should().Be("E-Commerce-Service");
        application.Owner.Should().Be("platform-team@company.com");
        application.Environments.Should().BeEmpty();

        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterApplication_WithDuplicateName_ShouldReturn400BadRequest()
    {
        // Arrange
        var command = TestDataGenerator.CreateProductionApplication("Payment-Service");
        await _client.PostAsJsonAsync("/api/applications", command);

        // Act - Try to register again with same name
        var response = await _client.PostAsJsonAsync("/api/applications", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetApplicationById_WithExistingId_ShouldReturn200Ok()
    {
        // Arrange
        var registerCommand = TestDataGenerator.CreateProductionApplication("User-Service");
        var registerResponse = await _client.PostAsJsonAsync("/api/applications", registerCommand);
        var registeredApp = await registerResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        // Act
        var response = await _client.GetAsync($"/api/applications/{registeredApp!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var application = await response.Content.ReadFromJsonAsync<ApplicationDto>();
        application.Should().NotBeNull();
        application!.Id.Should().Be(registeredApp.Id);
        application.Name.Should().Be("User-Service");
    }

    [Fact]
    public async Task GetApplicationById_WithNonExistentId_ShouldReturn400BadRequest()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/applications/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetApplicationByName_WithExistingName_ShouldReturn200Ok()
    {
        // Arrange
        var command = TestDataGenerator.CreateProductionApplication("Analytics-Service");
        await _client.PostAsJsonAsync("/api/applications", command);

        // Act
        var response = await _client.GetAsync("/api/applications/by-name/Analytics-Service");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var application = await response.Content.ReadFromJsonAsync<ApplicationDto>();
        application.Should().NotBeNull();
        application!.Name.Should().Be("Analytics-Service");
    }

    [Fact]
    public async Task AddEnvironmentConfig_ToProduction_ShouldConfigureCriticalRiskTier()
    {
        // Arrange - Register application
        var registerCommand = TestDataGenerator.CreateProductionApplication("API-Gateway");
        var registerResponse = await _client.PostAsJsonAsync("/api/applications", registerCommand);
        var application = await registerResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        // Act - Add production environment
        var envCommand = TestDataGenerator.CreateProductionEnvironment(application!.Id);
        var response = await _client.PostAsJsonAsync($"/api/applications/{application.Id}/environments", envCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedApp = await response.Content.ReadFromJsonAsync<ApplicationDto>();
        updatedApp.Should().NotBeNull();
        updatedApp!.Environments.Should().HaveCount(1);

        var prodEnv = updatedApp.Environments[0];
        prodEnv.Name.Should().Be("production");
        prodEnv.RiskTier.Should().Be("critical");
        prodEnv.SecurityTools.Should().Contain("snyk");
        prodEnv.SecurityTools.Should().Contain("prismacloud");
        prodEnv.PolicyReferences.Should().Contain("compliance/cicd/production");
    }

    [Fact]
    public async Task AddMultipleEnvironments_ShouldCreateCompleteApplicationProfile()
    {
        // Arrange - Register application
        var registerCommand = TestDataGenerator.CreateProductionApplication("Auth-Service");
        var registerResponse = await _client.PostAsJsonAsync("/api/applications", registerCommand);
        var application = await registerResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        // Act - Add all three environments
        var prodEnv = TestDataGenerator.CreateProductionEnvironment(application!.Id);
        var stagingEnv = TestDataGenerator.CreateStagingEnvironment(application.Id);
        var devEnv = TestDataGenerator.CreateDevelopmentEnvironment(application.Id);

        await _client.PostAsJsonAsync($"/api/applications/{application.Id}/environments", prodEnv);
        await _client.PostAsJsonAsync($"/api/applications/{application.Id}/environments", stagingEnv);
        var finalResponse = await _client.PostAsJsonAsync($"/api/applications/{application.Id}/environments", devEnv);

        // Assert
        var updatedApp = await finalResponse.Content.ReadFromJsonAsync<ApplicationDto>();
        updatedApp.Should().NotBeNull();
        updatedApp!.Environments.Should().HaveCount(3);

        // Verify production environment
        var production = updatedApp.Environments.First(e => e.Name == "production");
        production.RiskTier.Should().Be("critical");

        // Verify staging environment
        var staging = updatedApp.Environments.First(e => e.Name == "staging");
        staging.RiskTier.Should().Be("high");

        // Verify development environment
        var development = updatedApp.Environments.First(e => e.Name == "development");
        development.RiskTier.Should().Be("low");
    }

    [Fact]
    public async Task UpdateApplicationOwner_ShouldChangeOwner()
    {
        // Arrange
        var registerCommand = TestDataGenerator.CreateProductionApplication("Notification-Service");
        var registerResponse = await _client.PostAsJsonAsync("/api/applications", registerCommand);
        var application = await registerResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        var updateCommand = new UpdateApplicationOwnerCommand
        {
            ApplicationId = application!.Id,
            NewOwner = "new-platform-team@company.com"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/applications/{application.Id}/owner", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedApp = await response.Content.ReadFromJsonAsync<ApplicationDto>();
        updatedApp!.Owner.Should().Be("new-platform-team@company.com");
    }

    [Fact]
    public async Task DeactivateApplication_ShouldMarkAsInactive()
    {
        // Arrange
        var registerCommand = TestDataGenerator.CreateProductionApplication("Legacy-Service");
        var registerResponse = await _client.PostAsJsonAsync("/api/applications", registerCommand);
        var application = await registerResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/applications/{application!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetAllApplications_ShouldReturnPaginatedResults()
    {
        // Arrange - Register multiple applications
        await _client.PostAsJsonAsync("/api/applications", TestDataGenerator.CreateProductionApplication("App1"));
        await _client.PostAsJsonAsync("/api/applications", TestDataGenerator.CreateProductionApplication("App2"));
        await _client.PostAsJsonAsync("/api/applications", TestDataGenerator.CreateProductionApplication("App3"));

        // Act
        var response = await _client.GetAsync("/api/applications?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var applications = await response.Content.ReadFromJsonAsync<List<ApplicationDto>>();
        applications.Should().NotBeNull();
        applications.Should().HaveCountGreaterOrEqualTo(3);
    }
}
