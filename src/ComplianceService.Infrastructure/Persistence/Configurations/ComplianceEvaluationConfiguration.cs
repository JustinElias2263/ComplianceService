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

        builder.Property(e => e.ApplicationName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Environment)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.EvaluatedAt)
            .IsRequired();

        builder.Property(e => e.Passed)
            .IsRequired();

        // Store ScanResults as JSON (list of ScanResult value objects)
        builder.Property<string>("_scanResultsJson")
            .HasColumnName("ScanResults")
            .IsRequired();

        builder.Ignore(e => e.ScanResults);

        // Store PolicyDecision as JSON
        builder.Property<string>("_policyDecisionJson")
            .HasColumnName("PolicyDecision")
            .IsRequired();

        builder.Ignore(e => e.PolicyDecision);

        // Store AggregatedCounts as JSON
        builder.Property<string>("_aggregatedCountsJson")
            .HasColumnName("AggregatedCounts")
            .IsRequired();

        builder.Ignore(e => e.AggregatedCounts);

        // Indexes for querying
        builder.HasIndex(e => e.ApplicationId);
        builder.HasIndex(e => e.Environment);
        builder.HasIndex(e => e.EvaluatedAt);
        builder.HasIndex(e => e.Passed);

        // Composite index for common query pattern
        builder.HasIndex(e => new { e.ApplicationId, e.Environment, e.EvaluatedAt });

        // Ignore domain events
        builder.Ignore(e => e.DomainEvents);
    }
}
