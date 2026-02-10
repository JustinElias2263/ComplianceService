using ComplianceService.Domain.Audit;
using ComplianceService.Domain.Audit.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ComplianceService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for AuditLog aggregate root
/// Immutable, append-only audit trail
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EvaluationId)
            .IsRequired();

        builder.Property(a => a.ApplicationName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Environment)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.DecisionAllow)
            .IsRequired();

        // Store Violations as JSON array
        builder.Property<string>("_violationsJson")
            .HasColumnName("Violations")
            .IsRequired();

        builder.Ignore(a => a.Violations);

        builder.Property(a => a.PolicyPackage)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.TotalVulnerabilities)
            .IsRequired();

        builder.Property(a => a.CriticalCount)
            .IsRequired();

        builder.Property(a => a.HighCount)
            .IsRequired();

        builder.Property(a => a.InitiatedBy)
            .IsRequired()
            .HasMaxLength(200);

        // Store CompleteEvidence as JSON (entire evaluation evidence)
        builder.Property(a => a.CompleteEvidence)
            .HasConversion(
                v => v.JsonData,
                v => DecisionEvidence.Create(v).Value)
            .IsRequired()
            .HasColumnType("jsonb") // PostgreSQL JSONB for efficient querying
            .HasColumnName("CompleteEvidence");

        // Indexes for audit log queries
        builder.HasIndex(a => a.ApplicationName);
        builder.HasIndex(a => a.Environment);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.DecisionAllow);
        builder.HasIndex(a => a.EvaluationId);

        // Composite index for date range queries
        builder.HasIndex(a => new { a.ApplicationName, a.Environment, a.Timestamp });

        // Table partitioning hint (PostgreSQL - implemented at database level)
        builder.ToTable(t => t.HasComment("Partitioned by Timestamp (monthly) for scalability"));

        // Ignore domain events
        builder.Ignore(a => a.DomainEvents);
    }
}
