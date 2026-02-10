using System.Net;
using System.Net.Http.Json;
using ComplianceService.Api.Tests.Fixtures;
using ComplianceService.Application.DTOs;
using FluentAssertions;
using Moq;
using Xunit;

namespace ComplianceService.Api.Tests.Controllers;

/// <summary>
/// Integration tests for ComplianceController
/// Tests realistic compliance evaluation scenarios with various vulnerability levels
/// </summary>
public class ComplianceControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ComplianceControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Scenario_ProductionDeployment_CleanScan_ShouldAllow()
    {
        // Arrange - Register application and configure production environment
        var app = await RegisterApplicationWithEnvironment("PaymentService", "production", "critical");

        // Mock OPA to allow clean scans
        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = true,
                Violations = new List<string>(),
                Details = new Dictionary<string, object>
                {
                    ["message"] = "All compliance checks passed",
                    ["criticalCount"] = 0,
                    ["highCount"] = 0
                }
            });

        var command = TestDataGenerator.CreateCleanScan(app.Id, "production");

        // Act
        var response = await _client.PostAsJsonAsync("/api/compliance/evaluate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluation = await response.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();
        evaluation.Should().NotBeNull();
        evaluation!.IsBlocked.Should().BeFalse();
        evaluation.PolicyDecision.Allow.Should().BeTrue();
        evaluation.TotalVulnerabilityCount.Should().Be(0);
    }

    [Fact]
    public async Task Scenario_ProductionDeployment_CriticalVulnerabilities_ShouldBlock()
    {
        // Arrange - Register application with production environment
        var app = await RegisterApplicationWithEnvironment("ShoppingCart", "production", "critical");

        // Mock OPA to block critical vulnerabilities
        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = false,
                Violations = new List<string>
                {
                    "Critical vulnerabilities detected: 2",
                    "Production deployments require zero critical vulnerabilities"
                },
                Details = new Dictionary<string, object>
                {
                    ["criticalCount"] = 2,
                    ["highCount"] = 0,
                    ["riskTier"] = "critical"
                }
            });

        var command = TestDataGenerator.CreateScanWithCriticalVulnerabilities(app.Id, "production", 2);

        // Act
        var response = await _client.PostAsJsonAsync("/api/compliance/evaluate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluation = await response.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();
        evaluation.Should().NotBeNull();
        evaluation!.IsBlocked.Should().BeTrue();
        evaluation.PolicyDecision.Allow.Should().BeFalse();
        evaluation.PolicyDecision.Violations.Should().HaveCount(2);
        evaluation.CriticalVulnerabilityCount.Should().Be(2);
    }

    [Fact]
    public async Task Scenario_StagingDeployment_FewHighVulnerabilities_MayAllow()
    {
        // Arrange - Register application with staging environment
        var app = await RegisterApplicationWithEnvironment("ReportingService", "staging", "high");

        // Mock OPA to allow limited high vulnerabilities in staging
        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = true,
                Violations = new List<string>(),
                Details = new Dictionary<string, object>
                {
                    ["message"] = "Within acceptable threshold for staging",
                    ["criticalCount"] = 0,
                    ["highCount"] = 2,
                    ["highLimit"] = 3
                }
            });

        var command = TestDataGenerator.CreateScanWithHighVulnerabilities(app.Id, "staging", 2);

        // Act
        var response = await _client.PostAsJsonAsync("/api/compliance/evaluate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluation = await response.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();
        evaluation.Should().NotBeNull();
        evaluation!.IsBlocked.Should().BeFalse();
        evaluation.HighVulnerabilityCount.Should().Be(2);
    }

    [Fact]
    public async Task Scenario_DevelopmentDeployment_ManyVulnerabilities_ShouldAllow()
    {
        // Arrange - Register application with development environment (permissive)
        var app = await RegisterApplicationWithEnvironment("FeatureBranch", "development", "low");

        // Mock OPA to be permissive for development
        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = true,
                Violations = new List<string>(),
                Details = new Dictionary<string, object>
                {
                    ["message"] = "Development environment - permissive policy",
                    ["criticalCount"] = 1,
                    ["highCount"] = 5,
                    ["mediumCount"] = 10
                }
            });

        var command = TestDataGenerator.CreateScanWithMediumLowVulnerabilities(app.Id, "development");

        // Act
        var response = await _client.PostAsJsonAsync("/api/compliance/evaluate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluation = await response.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();
        evaluation.Should().NotBeNull();
        evaluation!.IsBlocked.Should().BeFalse();
        evaluation.PolicyDecision.Allow.Should().BeTrue();
    }

    [Fact]
    public async Task Scenario_MultiToolScan_ShouldAggregateResults()
    {
        // Arrange - Register application with production environment
        var app = await RegisterApplicationWithEnvironment("MicroserviceA", "production", "critical");

        // Mock OPA decision
        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = true,
                Violations = new List<string>(),
                Details = new Dictionary<string, object>
                {
                    ["message"] = "All tools passed",
                    ["toolsExecuted"] = new[] { "snyk", "prismacloud" }
                }
            });

        var command = TestDataGenerator.CreateMultiToolScan(app.Id, "production");

        // Act
        var response = await _client.PostAsJsonAsync("/api/compliance/evaluate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluation = await response.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();
        evaluation.Should().NotBeNull();
        evaluation!.ScanResults.Should().HaveCount(2);
        evaluation.ScanResults.Should().Contain(sr => sr.Tool == "snyk");
        evaluation.ScanResults.Should().Contain(sr => sr.Tool == "prismacloud");
    }

    [Fact]
    public async Task Scenario_RealWorldProductionDeployment_ShouldEvaluateComprehensively()
    {
        // Arrange - Simulate a real microservice deployment
        var app = await RegisterApplicationWithEnvironment("OrderProcessingService", "production", "critical");

        // Mock realistic OPA decision
        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = true,
                Violations = new List<string>(),
                Details = new Dictionary<string, object>
                {
                    ["policyEvaluatedAt"] = DateTime.UtcNow,
                    ["policyPackage"] = "compliance/cicd/production",
                    ["criticalCount"] = 0,
                    ["highCount"] = 0,
                    ["mediumCount"] = 0,
                    ["lowCount"] = 2,
                    ["requiredToolsPresent"] = true
                }
            });

        var command = TestDataGenerator.CreateRealisticProductionDeployment(app.Id);

        // Act
        var response = await _client.PostAsJsonAsync("/api/compliance/evaluate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluation = await response.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();
        evaluation.Should().NotBeNull();
        evaluation!.IsBlocked.Should().BeFalse();
        evaluation.ApplicationId.Should().Be(app.Id);
        evaluation.Environment.Should().Be("production");
        evaluation.LowVulnerabilityCount.Should().Be(2);
    }

    [Fact]
    public async Task Scenario_GetEvaluationById_ShouldReturnStoredEvaluation()
    {
        // Arrange - First create an evaluation
        var app = await RegisterApplicationWithEnvironment("DataPipeline", "production", "critical");

        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = true,
                Violations = new List<string>()
            });

        var evaluateCommand = TestDataGenerator.CreateCleanScan(app.Id, "production");
        var evaluateResponse = await _client.PostAsJsonAsync("/api/compliance/evaluate", evaluateCommand);
        var createdEvaluation = await evaluateResponse.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();

        // Act - Retrieve the evaluation
        var response = await _client.GetAsync($"/api/compliance/evaluations/{createdEvaluation!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluation = await response.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();
        evaluation.Should().NotBeNull();
        evaluation!.Id.Should().Be(createdEvaluation.Id);
    }

    [Fact]
    public async Task Scenario_GetEvaluationsByApplication_ShouldReturnHistory()
    {
        // Arrange - Create multiple evaluations for same application
        var app = await RegisterApplicationWithEnvironment("MLService", "production", "critical");

        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = true,
                Violations = new List<string>()
            });

        // Create 3 evaluations
        await _client.PostAsJsonAsync("/api/compliance/evaluate", TestDataGenerator.CreateCleanScan(app.Id));
        await _client.PostAsJsonAsync("/api/compliance/evaluate", TestDataGenerator.CreateCleanScan(app.Id));
        await _client.PostAsJsonAsync("/api/compliance/evaluate", TestDataGenerator.CreateCleanScan(app.Id));

        // Act - Get evaluation history
        var response = await _client.GetAsync($"/api/compliance/evaluations/application/{app.Id}?pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluations = await response.Content.ReadFromJsonAsync<List<ComplianceEvaluationDto>>();
        evaluations.Should().NotBeNull();
        evaluations.Should().HaveCountGreaterOrEqualTo(3);
        evaluations!.All(e => e.ApplicationId == app.Id).Should().BeTrue();
    }

    /// <summary>
    /// Helper method to register an application with a specific environment
    /// </summary>
    private async Task<ApplicationDto> RegisterApplicationWithEnvironment(
        string appName,
        string environmentName,
        string riskTier)
    {
        // Register application
        var registerCommand = TestDataGenerator.CreateProductionApplication(appName);
        var registerResponse = await _client.PostAsJsonAsync("/api/applications", registerCommand);
        var app = await registerResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        // Add environment
        var envCommand = new Application.Commands.AddEnvironmentConfigCommand
        {
            ApplicationId = app!.Id,
            EnvironmentName = environmentName,
            RiskTier = riskTier,
            SecurityTools = new List<string> { "snyk", "prismacloud" },
            PolicyReferences = new List<string> { $"compliance/cicd/{environmentName}" }
        };

        await _client.PostAsJsonAsync($"/api/applications/{app.Id}/environments", envCommand);

        return app;
    }
}
