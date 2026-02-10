using ComplianceService.Domain.Audit;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace ComplianceService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Entity Framework Core implementation of IAuditLogRepository
/// Audit logs are append-only for compliance purposes
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly ApplicationDbContext _context;

    public AuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AuditLog>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (auditLog == null)
            return Result.Failure<AuditLog>($"Audit log with ID {id} not found");

        return Result.Success(auditLog);
    }

    public async Task<Result<AuditLog>> GetByEvaluationIdAsync(string evaluationId, CancellationToken cancellationToken = default)
    {
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(a => a.EvaluationId == evaluationId, cancellationToken);

        if (auditLog == null)
            return Result.Failure<AuditLog>($"Audit log with evaluation ID {evaluationId} not found");

        return Result.Success(auditLog);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByApplicationIdAsync(
        Guid applicationId,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.ApplicationId == applicationId)
            .OrderByDescending(a => a.EvaluatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByApplicationAndEnvironmentAsync(
        Guid applicationId,
        string environment,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEnvironment = environment.Trim().ToLowerInvariant();
        var query = _context.AuditLogs
            .Where(a => a.ApplicationId == applicationId && a.Environment == normalizedEnvironment);

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.EvaluatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.EvaluatedAt <= toDate.Value);
        }

        return await query
            .OrderByDescending(a => a.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetBlockedDecisionsAsync(
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .Where(a => !a.Allowed);

        if (since.HasValue)
        {
            query = query.Where(a => a.EvaluatedAt >= since.Value);
        }

        query = query.OrderByDescending(a => a.EvaluatedAt);

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetWithCriticalVulnerabilitiesAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .Where(a => a.CriticalCount > 0);

        if (since.HasValue)
        {
            query = query.Where(a => a.EvaluatedAt >= since.Value);
        }

        return await query
            .OrderByDescending(a => a.CriticalCount)
            .ThenByDescending(a => a.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByRiskTierAsync(
        string riskTier,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRiskTier = riskTier.Trim().ToLowerInvariant();
        var query = _context.AuditLogs
            .Where(a => a.RiskTier == normalizedRiskTier);

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.EvaluatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.EvaluatedAt <= toDate.Value);
        }

        return await query
            .OrderByDescending(a => a.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AuditStatistics> GetStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.EvaluatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.EvaluatedAt <= toDate.Value);
        }

        var logs = await query.ToListAsync(cancellationToken);

        var totalEvaluations = logs.Count;
        var allowedCount = logs.Count(a => a.Allowed);
        var blockedCount = logs.Count(a => !a.Allowed);
        var blockedPercentage = totalEvaluations > 0 ? (double)blockedCount / totalEvaluations * 100 : 0;

        var totalCriticalVulnerabilities = logs.Sum(a => a.CriticalCount);
        var totalHighVulnerabilities = logs.Sum(a => a.HighCount);

        var evaluationsByEnvironment = logs
            .GroupBy(a => a.Environment)
            .ToDictionary(g => g.Key, g => g.Count());

        var evaluationsByRiskTier = logs
            .GroupBy(a => a.RiskTier)
            .ToDictionary(g => g.Key, g => g.Count());

        return new AuditStatistics(
            totalEvaluations,
            allowedCount,
            blockedCount,
            blockedPercentage,
            totalCriticalVulnerabilities,
            totalHighVulnerabilities,
            evaluationsByEnvironment,
            evaluationsByRiskTier);
    }

    public async Task<Result> AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.AuditLogs.AddAsync(auditLog, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to add audit log: {ex.Message}");
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
