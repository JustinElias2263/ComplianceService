using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.Audit.Interfaces;

/// <summary>
/// Repository interface for AuditLog aggregate
/// Audit logs are append-only for compliance purposes
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Get audit log by ID
    /// </summary>
    Task<Result<AuditLog>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit log by evaluation ID
    /// </summary>
    Task<Result<AuditLog>> GetByEvaluationIdAsync(string evaluationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs for an application
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByApplicationIdAsync(
        Guid applicationId,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs for an application and environment
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByApplicationAndEnvironmentAsync(
        Guid applicationId,
        string environment,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get blocked decisions (where allowed=false)
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetBlockedDecisionsAsync(
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs with critical vulnerabilities
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetWithCriticalVulnerabilitiesAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs by risk tier
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByRiskTierAsync(
        string riskTier,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit statistics
    /// </summary>
    Task<AuditStatistics> GetStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add new audit log (append-only)
    /// </summary>
    Task<Result> AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save changes (unit of work)
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Audit statistics for reporting
/// </summary>
public record AuditStatistics(
    int TotalEvaluations,
    int AllowedCount,
    int BlockedCount,
    double BlockedPercentage,
    int TotalCriticalVulnerabilities,
    int TotalHighVulnerabilities,
    Dictionary<string, int> EvaluationsByEnvironment,
    Dictionary<string, int> EvaluationsByRiskTier);
