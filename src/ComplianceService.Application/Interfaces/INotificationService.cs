namespace ComplianceService.Application.Interfaces;

/// <summary>
/// Interface for notification service
/// Implemented in Infrastructure layer (email, Slack, webhooks, etc.)
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends notification when compliance evaluation completes
    /// </summary>
    /// <param name="applicationName">Application name</param>
    /// <param name="environment">Environment name</param>
    /// <param name="passed">Whether compliance check passed</param>
    /// <param name="violations">List of policy violations if any</param>
    /// <param name="recipients">Notification recipients</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendComplianceNotificationAsync(
        string applicationName,
        string environment,
        bool passed,
        IReadOnlyList<string> violations,
        IReadOnlyList<string> recipients,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends notification when critical vulnerabilities are detected
    /// </summary>
    /// <param name="applicationName">Application name</param>
    /// <param name="environment">Environment name</param>
    /// <param name="criticalCount">Number of critical vulnerabilities</param>
    /// <param name="highCount">Number of high vulnerabilities</param>
    /// <param name="recipients">Notification recipients</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendCriticalVulnerabilityAlertAsync(
        string applicationName,
        string environment,
        int criticalCount,
        int highCount,
        IReadOnlyList<string> recipients,
        CancellationToken cancellationToken = default);
}
