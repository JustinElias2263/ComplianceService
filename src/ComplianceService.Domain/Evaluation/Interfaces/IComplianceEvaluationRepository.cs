using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.Evaluation.Interfaces;

/// <summary>
/// Repository interface for ComplianceEvaluation aggregate
/// </summary>
public interface IComplianceEvaluationRepository
{
    /// <summary>
    /// Get evaluation by ID
    /// </summary>
    Task<Result<ComplianceEvaluation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get evaluations for an application
    /// </summary>
    Task<IReadOnlyList<ComplianceEvaluation>> GetByApplicationIdAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get evaluations for an application and environment
    /// </summary>
    Task<IReadOnlyList<ComplianceEvaluation>> GetByApplicationAndEnvironmentAsync(
        Guid applicationId,
        string environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent evaluations (last N days)
    /// </summary>
    Task<IReadOnlyList<ComplianceEvaluation>> GetRecentAsync(
        int days = 7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get blocked evaluations (where decision was denied)
    /// </summary>
    Task<IReadOnlyList<ComplianceEvaluation>> GetBlockedEvaluationsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add new evaluation
    /// </summary>
    Task<Result> AddAsync(ComplianceEvaluation evaluation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save changes (unit of work)
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
