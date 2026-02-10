using DomainApplication = ComplianceService.Domain.ApplicationProfile.Application;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using ComplianceService.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace ComplianceService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Entity Framework Core implementation of IApplicationRepository
/// </summary>
public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _context;

    public ApplicationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DomainApplication>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await _context.Applications
            .Include(a => a.Environments)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return application is null
            ? Result.Failure<DomainApplication>("Application not found")
            : Result.Success(application);
    }

    public async Task<Result<DomainApplication>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var application = await _context.Applications
            .Include(a => a.Environments)
            .FirstOrDefaultAsync(a => a.Name == name, cancellationToken);

        return application is null
            ? Result.Failure<DomainApplication>("Application not found")
            : Result.Success(application);
    }

    public async Task<IReadOnlyList<DomainApplication>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Environments)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DomainApplication>> GetByRiskTierAsync(string riskTier, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Environments)
            .Where(a => a.Environments.Any(e => e.RiskTier.Value == riskTier.ToLowerInvariant()))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Applications.Where(a => a.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(a => a.Id != excludeId.Value);
        }

        return !await query.AnyAsync(cancellationToken);
    }

    public async Task<Result> AddAsync(DomainApplication application, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Applications.AddAsync(application, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to add application: {ex.Message}");
        }
    }

    public Task<Result> UpdateAsync(DomainApplication application, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Applications.Update(application);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure($"Failed to update application: {ex.Message}"));
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await _context.Applications.FindAsync(new object[] { id }, cancellationToken);

        if (application is null)
        {
            return Result.Failure("Application not found");
        }

        try
        {
            application.Deactivate();
            _context.Applications.Update(application);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete application: {ex.Message}");
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
