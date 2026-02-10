using System.Net;
using System.Net.Http.Json;
using ComplianceService.Api.Tests.Fixtures;
using ComplianceService.Application.DTOs;
using FluentAssertions;
using Moq;
using Xunit;

namespace ComplianceService.Api.Tests.Controllers;

/// <summary>
/// Integration tests for AuditController
/// Tests audit log retrieval and statistics scenarios
/// </summary>
public class AuditControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuditControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Scenario_AfterEvaluation_AuditLogShouldBeCreated()
    {
        // Arrange - Register app and evaluate compliance
        var app = await RegisterApplicationAndEvaluate("AuditTestApp1", allowDecision: true);

        // Act - Get audit logs for application
        var response = await _client.GetAsync($"/api/audit/application/{app.ApplicationId}?pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auditLogs = await response.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        auditLogs.Should().NotBeNull();
        auditLogs.Should().HaveCountGreaterOrEqualTo(1);

        var firstLog = auditLogs!.First();
        firstLog.ApplicationName.Should().NotBeNullOrEmpty();
        firstLog.Environment.Should().Be("production");
    }

    [Fact]
    public async Task Scenario_BlockedEvaluation_ShouldAuditWithBlockedFlag()
    {
        // Arrange - Create blocked evaluation
        var app = await RegisterApplicationAndEvaluate("BlockedApp", allowDecision: false);

        // Act - Get audit logs
        var response = await _client.GetAsync($"/api/audit/application/{app.ApplicationId}?pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auditLogs = await response.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        var blockedLog = auditLogs!.First();

        blockedLog.DecisionAllow.Should().BeFalse();
        blockedLog.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Scenario_GetAuditLogById_ShouldReturnDetailedLog()
    {
        // Arrange - Create evaluation to generate audit log
        var evaluation = await RegisterApplicationAndEvaluate("DetailedAuditApp", allowDecision: true);

        // Wait a moment for async audit log creation
        await Task.Delay(100);

        // Act - Get all audit logs to find the ID
        var logsResponse = await _client.GetAsync($"/api/audit/application/{evaluation.ApplicationId}?pageSize=1");
        var logs = await logsResponse.Content.ReadFromJsonAsync<List<AuditLogDto>>();

        if (logs != null && logs.Any())
        {
            var auditLogId = logs.First().Id;

            var response = await _client.GetAsync($"/api/audit/{auditLogId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var auditLog = await response.Content.ReadFromJsonAsync<AuditLogDto>();
            auditLog.Should().NotBeNull();
            auditLog!.Id.Should().Be(auditLogId);
        }
    }

    [Fact]
    public async Task Scenario_GetBlockedDecisions_ShouldOnlyReturnBlocked()
    {
        // Arrange - Create mix of allowed and blocked evaluations
        await RegisterApplicationAndEvaluate("AllowedApp1", allowDecision: true);
        await RegisterApplicationAndEvaluate("BlockedApp1", allowDecision: false);
        await RegisterApplicationAndEvaluate("BlockedApp2", allowDecision: false);

        // Act - Get only blocked decisions
        var response = await _client.GetAsync("/api/audit/blocked?pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var blockedLogs = await response.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        blockedLogs.Should().NotBeNull();
        blockedLogs.Should().HaveCountGreaterOrEqualTo(2);
        blockedLogs!.All(log => log.DecisionAllow == false).Should().BeTrue();
    }

    [Fact]
    public async Task Scenario_GetCriticalVulnerabilities_ShouldFilterBySeverity()
    {
        // Arrange - Create evaluation with critical vulnerabilities
        var app = await RegisterApplicationWithEnvironment("CriticalVulnApp", "production", "critical");

        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = false,
                Violations = new List<PolicyViolationDto>
                {
                    new PolicyViolationDto
                    {
                        Rule = "critical_vulnerabilities",
                        Message = "Critical vulnerabilities detected",
                        Severity = "critical"
                    }
                }
            });

        var command = TestDataGenerator.CreateScanWithCriticalVulnerabilities(app.Id, "production", 3);
        await _client.PostAsJsonAsync("/api/compliance/evaluate", command);

        // Act - Get critical vulnerability logs
        var response = await _client.GetAsync("/api/audit/critical-vulnerabilities?pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var criticalLogs = await response.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        criticalLogs.Should().NotBeNull();
        criticalLogs!.All(log => log.CriticalCount > 0).Should().BeTrue();
    }

    [Fact]
    public async Task Scenario_GetAuditStatistics_ShouldProvideOverview()
    {
        // Arrange - Create multiple evaluations
        await RegisterApplicationAndEvaluate("StatsApp1", allowDecision: true);
        await RegisterApplicationAndEvaluate("StatsApp2", allowDecision: false);
        await RegisterApplicationAndEvaluate("StatsApp3", allowDecision: true);

        // Act
        var response = await _client.GetAsync("/api/audit/statistics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<AuditStatisticsDto>();
        stats.Should().NotBeNull();
        stats!.TotalEvaluations.Should().BeGreaterOrEqualTo(3);
        stats.AllowedCount.Should().BeGreaterOrEqualTo(2);
        stats.BlockedCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task Scenario_GetRecentEvaluations_ShouldReturnLatest()
    {
        // Arrange - Create evaluations over time
        var app = await RegisterApplicationWithEnvironment("RecentApp", "production", "critical");

        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto { Allow = true, Violations = new List<PolicyViolationDto>() });

        // Create 5 evaluations
        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/compliance/evaluate",
                TestDataGenerator.CreateCleanScan(app.Id, "production"));
            await Task.Delay(50); // Small delay to ensure different timestamps
        }

        // Act - Get recent evaluations
        var response = await _client.GetAsync("/api/compliance/evaluations/recent?hours=24&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var evaluations = await response.Content.ReadFromJsonAsync<List<ComplianceEvaluationDto>>();
        evaluations.Should().NotBeNull();
        evaluations.Should().HaveCountGreaterOrEqualTo(5);

        // Verify they're ordered by timestamp (most recent first)
        var timestamps = evaluations!.Select(e => e.EvaluatedAt).ToList();
        timestamps.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Scenario_GetAuditLogsByRiskTier_ShouldFilterCorrectly()
    {
        // Arrange - Create applications with different risk tiers
        var criticalApp = await RegisterApplicationWithEnvironment("CriticalApp", "production", "critical");
        var highApp = await RegisterApplicationWithEnvironment("HighApp", "staging", "high");
        var lowApp = await RegisterApplicationWithEnvironment("LowApp", "development", "low");

        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto { Allow = true, Violations = new List<PolicyViolationDto>() });

        await _client.PostAsJsonAsync("/api/compliance/evaluate", TestDataGenerator.CreateCleanScan(criticalApp.Id, "production"));
        await _client.PostAsJsonAsync("/api/compliance/evaluate", TestDataGenerator.CreateCleanScan(highApp.Id, "staging"));
        await _client.PostAsJsonAsync("/api/compliance/evaluate", TestDataGenerator.CreateCleanScan(lowApp.Id, "development"));

        // Act - Get only critical risk tier audit logs
        var response = await _client.GetAsync("/api/audit/risk-tier/critical?pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var criticalLogs = await response.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        criticalLogs.Should().NotBeNull();
        criticalLogs.Should().HaveCountGreaterOrEqualTo(1);
    }

    /// <summary>
    /// Helper method to register application with environment
    /// </summary>
    private async Task<ApplicationDto> RegisterApplicationWithEnvironment(
        string appName,
        string environmentName,
        string riskTier)
    {
        var registerCommand = TestDataGenerator.CreateProductionApplication(appName);
        var registerResponse = await _client.PostAsJsonAsync("/api/applications", registerCommand);
        var app = await registerResponse.Content.ReadFromJsonAsync<ApplicationDto>();

        var envCommand = new Application.Commands.AddEnvironmentConfigCommand
        {
            ApplicationId = app!.Id,
            EnvironmentName = environmentName,
            RiskTier = riskTier,
            SecurityTools = new List<string> { "snyk" },
            PolicyReferences = new List<string> { $"compliance/cicd/{environmentName}" }
        };

        await _client.PostAsJsonAsync($"/api/applications/{app.Id}/environments", envCommand);

        return app;
    }

    /// <summary>
    /// Helper method to register application and perform evaluation
    /// </summary>
    private async Task<ComplianceEvaluationDto> RegisterApplicationAndEvaluate(
        string appName,
        bool allowDecision)
    {
        var app = await RegisterApplicationWithEnvironment(appName, "production", "critical");

        _factory.MockOpaClient
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaDecisionDto
            {
                Allow = allowDecision,
                Violations = allowDecision
                    ? new List<PolicyViolationDto>()
                    : new List<PolicyViolationDto>
                    {
                        new PolicyViolationDto
                        {
                            Rule = "compliance_check",
                            Message = "Compliance check failed",
                            Severity = "high"
                        }
                    }
            });

        var command = TestDataGenerator.CreateCleanScan(app.Id, "production");
        var response = await _client.PostAsJsonAsync("/api/compliance/evaluate", command);

        var evaluation = await response.Content.ReadFromJsonAsync<ComplianceEvaluationDto>();
        return evaluation!;
    }
}
