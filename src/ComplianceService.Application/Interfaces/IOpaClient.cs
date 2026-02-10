using ComplianceService.Application.DTOs;

namespace ComplianceService.Application.Interfaces;

/// <summary>
/// Interface for Open Policy Agent (OPA) client
/// Implemented in Infrastructure layer as a sidecar HTTP client
/// </summary>
public interface IOpaClient
{
    /// <summary>
    /// Evaluates compliance policy against scan results
    /// </summary>
    /// <param name="input">OPA input containing application context and scan results</param>
    /// <param name="policyPackage">OPA policy package name (e.g., "compliance.cicd.production")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Policy decision with allow/deny and violations</returns>
    Task<OpaDecisionDto> EvaluatePolicyAsync(
        OpaInputDto input,
        string policyPackage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if OPA sidecar is healthy and reachable
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
