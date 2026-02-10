using ComplianceService.Domain.Evaluation;
using ComplianceService.Domain.Evaluation.Interfaces;
using ComplianceService.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace ComplianceService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Entity Framework Core implementation of IComplianceEvaluationRepository
/// </summary>
public class ComplianceEvaluationRepository : IComplianceEvaluationRepository
{
    private readonly ApplicationDbContext _context;

    public ComplianceEvaluationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ComplianceEvaluation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evaluation = await _context.ComplianceEvaluations
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (evaluation == null)
            return Result.Failure<ComplianceEvaluation>($"Compliance evaluation with ID {id} not found");

        return Result.Success(evaluation);
    }

    public async Task<IReadOnlyList<ComplianceEvaluation>> GetByApplicationIdAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ComplianceEvaluations
            .Where(e => e.ApplicationId == applicationId)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceEvaluation>> GetByApplicationAndEnvironmentAsync(
        Guid applicationId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var normalizedEnvironment = environment.Trim().ToLowerInvariant();

        return await _context.ComplianceEvaluations
            .Where(e => e.ApplicationId == applicationId && e.Environment == normalizedEnvironment)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceEvaluation>> GetRecentAsync(
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        return await _context.ComplianceEvaluations
            .Where(e => e.EvaluatedAt >= cutoffDate)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplianceEvaluation>> GetBlockedEvaluationsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ComplianceEvaluations
            .Where(e => !e.Decision.Allowed);

        if (since.HasValue)
        {
            query = query.Where(e => e.EvaluatedAt >= since.Value);
        }

        return await query
            .OrderByDescending(e => e.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> AddAsync(ComplianceEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ComplianceEvaluations.AddAsync(evaluation, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to add compliance evaluation: {ex.Message}");
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
