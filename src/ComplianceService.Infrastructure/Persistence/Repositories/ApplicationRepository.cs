using ComplianceService.Domain.ApplicationProfile;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
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

    public async Task<Application?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Environments)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Application?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Environments)
            .FirstOrDefaultAsync(a => a.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<Application>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Environments)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Application>> GetByOwnerAsync(string owner, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Environments)
            .Where(a => a.Owner == owner)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Application>> GetActiveApplicationsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Environments)
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Application application, CancellationToken cancellationToken = default)
    {
        await _context.Applications.AddAsync(application, cancellationToken);
    }

    public Task UpdateAsync(Application application, CancellationToken cancellationToken = default)
    {
        _context.Applications.Update(application);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Application application, CancellationToken cancellationToken = default)
    {
        _context.Applications.Remove(application);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .AnyAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Applications.Where(a => a.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(a => a.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Applications.CountAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
