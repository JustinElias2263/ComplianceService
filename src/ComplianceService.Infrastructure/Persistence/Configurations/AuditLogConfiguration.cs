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

        builder.Property(a => a.ApplicationId)
            .IsRequired();

        builder.Property(a => a.ApplicationName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Environment)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.EvaluatedAt)
            .IsRequired();

        builder.Property(a => a.Allowed)
            .IsRequired();

        // Store Violations as JSON array
        builder.Property<string>("_violationsJson")
            .HasColumnName("Violations")
            .IsRequired();

        builder.Ignore(a => a.Violations);

        builder.Property(a => a.TotalVulnerabilityCount)
            .IsRequired();

        builder.Property(a => a.CriticalCount)
            .IsRequired();

        builder.Property(a => a.HighCount)
            .IsRequired();

        builder.Property(a => a.MediumCount)
            .IsRequired();

        builder.Property(a => a.LowCount)
            .IsRequired();

        builder.Property(a => a.RiskTier)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.EvaluationDurationMs)
            .IsRequired();

        // Store Evidence as JSON (entire evaluation evidence)
        builder.Property(a => a.Evidence)
            .HasConversion(
                v => JsonSerializer.Serialize(new
                {
                    ScanResultsJson = v.ScanResultsJson,
                    PolicyInputJson = v.PolicyInputJson,
                    PolicyOutputJson = v.PolicyOutputJson,
                    CapturedAt = v.CapturedAt
                }, (JsonSerializerOptions?)null),
                v => DecisionEvidence.Create(
                    JsonDocument.Parse(v).RootElement.GetProperty("ScanResultsJson").GetString()!,
                    JsonDocument.Parse(v).RootElement.GetProperty("PolicyInputJson").GetString()!,
                    JsonDocument.Parse(v).RootElement.GetProperty("PolicyOutputJson").GetString()!).Value)
            .IsRequired()
            .HasColumnType("jsonb") // PostgreSQL JSONB for efficient querying
            .HasColumnName("Evidence");

        // Indexes for audit log queries
        builder.HasIndex(a => a.ApplicationName);
        builder.HasIndex(a => a.Environment);
        builder.HasIndex(a => a.EvaluatedAt);
        builder.HasIndex(a => a.Allowed);
        builder.HasIndex(a => a.EvaluationId);

        // Composite index for date range queries
        builder.HasIndex(a => new { a.ApplicationName, a.Environment, a.EvaluatedAt });

        // Table partitioning hint (PostgreSQL - implemented at database level)
        builder.ToTable(t => t.HasComment("Partitioned by EvaluatedAt (monthly) for scalability"));

        // Ignore domain events
        builder.Ignore(a => a.DomainEvents);
    }
}
