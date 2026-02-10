using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.ApplicationProfile.Interfaces;

/// <summary>
/// Repository interface for Application aggregate
/// Defines persistence operations without implementation details
/// </summary>
public interface IApplicationRepository
{
    /// <summary>
    /// Get application by ID
    /// </summary>
    Task<Result<Application>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get application by name
    /// </summary>
    Task<Result<Application>> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all applications
    /// </summary>
    Task<IReadOnlyList<Application>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get applications by risk tier
    /// </summary>
    Task<IReadOnlyList<Application>> GetByRiskTierAsync(string riskTier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if application name is unique
    /// </summary>
    Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add new application
    /// </summary>
    Task<Result> AddAsync(Application application, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing application
    /// </summary>
    Task<Result> UpdateAsync(Application application, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete application (soft delete - mark as inactive)
    /// </summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save changes (unit of work)
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
