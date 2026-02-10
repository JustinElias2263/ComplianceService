using ComplianceService.Domain.ApplicationProfile;
using ComplianceService.Domain.Audit;
using ComplianceService.Domain.Evaluation;
using ComplianceService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceService.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for ComplianceService
/// Manages all aggregate roots and their persistence
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // DbSets for aggregate roots
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ComplianceEvaluation> ComplianceEvaluations => Set<ComplianceEvaluation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations
        modelBuilder.ApplyConfiguration(new ApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new EnvironmentConfigConfiguration());
        modelBuilder.ApplyConfiguration(new ComplianceEvaluationConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

        // PostgreSQL specific configurations
        modelBuilder.HasDefaultSchema("compliance");
    }
}
