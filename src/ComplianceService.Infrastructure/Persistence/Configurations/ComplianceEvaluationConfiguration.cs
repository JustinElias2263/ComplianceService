using ComplianceService.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ComplianceService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for ComplianceEvaluation aggregate root
/// </summary>
public class ComplianceEvaluationConfiguration : IEntityTypeConfiguration<ComplianceEvaluation>
{
    public void Configure(EntityTypeBuilder<ComplianceEvaluation> builder)
    {
        builder.ToTable("ComplianceEvaluations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ApplicationId)
            .IsRequired();

        builder.Property(e => e.Environment)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.RiskTier)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.EvaluatedAt)
            .IsRequired();

        // Store ScanResults as JSON (list of ScanResult value objects)
        builder.Property<string>("_scanResultsJson")
            .HasColumnName("ScanResults")
            .IsRequired();

        builder.Ignore(e => e.ScanResults);

        // Store Decision as JSON
        builder.Property<string>("_decisionJson")
            .HasColumnName("Decision")
            .IsRequired();

        builder.Ignore(e => e.Decision);

        // Indexes for querying
        builder.HasIndex(e => e.ApplicationId);
        builder.HasIndex(e => e.Environment);
        builder.HasIndex(e => e.EvaluatedAt);

        // Composite index for common query pattern
        builder.HasIndex(e => new { e.ApplicationId, e.Environment, e.EvaluatedAt });

        // Ignore domain events
        builder.Ignore(e => e.DomainEvents);
    }
}
