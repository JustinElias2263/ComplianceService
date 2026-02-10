using ComplianceService.Application.DTOs;
using ComplianceService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace ComplianceService.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client implementation for Open Policy Agent (OPA) sidecar communication
/// Sends policy evaluation requests to OPA and receives compliance decisions
/// </summary>
public class OpaHttpClient : IOpaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpaHttpClient> _logger;
    private readonly string _opaBaseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpaHttpClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpaHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _opaBaseUrl = configuration["OpaSettings:BaseUrl"] ?? "http://localhost:8181";

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _logger.LogInformation("OPA HTTP Client initialized with base URL: {OpaBaseUrl}", _opaBaseUrl);
    }

    public async Task<OpaDecisionDto> EvaluatePolicyAsync(
        OpaInputDto input,
        string policyPackage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Evaluating policy {PolicyPackage} for application {ApplicationName} in environment {Environment}",
                policyPackage,
                input.Application.Name,
                input.Application.Environment);

            // OPA Data API endpoint: /v1/data/{policy-package}
            var endpoint = $"{_opaBaseUrl}/v1/data/{policyPackage.Replace('.', '/')}";

            // Construct OPA request payload
            var opaRequest = new
            {
                input = input
            };

            var response = await _httpClient.PostAsJsonAsync(
                endpoint,
                opaRequest,
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "OPA policy evaluation failed with status {StatusCode}: {ErrorContent}",
                    response.StatusCode,
                    errorContent);

                throw new HttpRequestException(
                    $"OPA request failed with status {response.StatusCode}: {errorContent}");
            }

            var opaResponse = await response.Content.ReadFromJsonAsync<OpaResponse>(
                _jsonOptions,
                cancellationToken);

            if (opaResponse?.Result == null)
            {
                _logger.LogError("OPA response did not contain expected result structure");
                throw new InvalidOperationException("Invalid OPA response structure");
            }

            // Parse OPA result into decision DTO
            var decision = ParseOpaResult(opaResponse.Result);

            _logger.LogInformation(
                "Policy evaluation completed: Allow={Allow}, Violations={ViolationCount}",
                decision.Allow,
                decision.Violations.Count);

            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error evaluating policy {PolicyPackage} for application {ApplicationName}",
                policyPackage,
                input.Application.Name);
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var healthEndpoint = $"{_opaBaseUrl}/health";
            var response = await _httpClient.GetAsync(healthEndpoint, cancellationToken);

            var isHealthy = response.IsSuccessStatusCode;

            _logger.LogInformation("OPA health check: {Status}", isHealthy ? "Healthy" : "Unhealthy");

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPA health check failed");
            return false;
        }
    }

    private OpaDecisionDto ParseOpaResult(Dictionary<string, object> result)
    {
        // Expected OPA result structure:
        // {
        //   "allow": true/false,
        //   "violations": [ ... ],
        //   "reason": "..."
        // }

        var allow = false;
        var violations = new List<PolicyViolationDto>();
        string? reason = null;

        if (result.TryGetValue("allow", out var allowValue))
        {
            allow = Convert.ToBoolean(allowValue);
        }

        if (result.TryGetValue("violations", out var violationsValue))
        {
            if (violationsValue is JsonElement violationsElement && violationsElement.ValueKind == JsonValueKind.Array)
            {
                violations = ParseViolations(violationsElement);
            }
        }

        if (result.TryGetValue("reason", out var reasonValue))
        {
            reason = reasonValue?.ToString();
        }

        return new OpaDecisionDto
        {
            Allow = allow,
            Violations = violations,
            Reason = reason ?? (allow ? "All policy checks passed" : "Policy violations detected"),
            RawResult = result
        };
    }

    private List<PolicyViolationDto> ParseViolations(JsonElement violationsElement)
    {
        var violations = new List<PolicyViolationDto>();

        foreach (var violation in violationsElement.EnumerateArray())
        {
            try
            {
                var rule = violation.GetProperty("rule").GetString() ?? "unknown";
                var message = violation.GetProperty("message").GetString() ?? "No message provided";
                var severity = violation.GetProperty("severity").GetString() ?? "medium";

                Dictionary<string, object>? details = null;
                if (violation.TryGetProperty("details", out var detailsElement))
                {
                    details = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        detailsElement.GetRawText(),
                        _jsonOptions);
                }

                violations.Add(new PolicyViolationDto
                {
                    Rule = rule,
                    Message = message,
                    Severity = severity,
                    Details = details
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse OPA violation: {Violation}", violation.GetRawText());
            }
        }

        return violations;
    }

    /// <summary>
    /// OPA HTTP response structure
    /// </summary>
    private class OpaResponse
    {
        public Dictionary<string, object>? Result { get; set; }
    }
}
