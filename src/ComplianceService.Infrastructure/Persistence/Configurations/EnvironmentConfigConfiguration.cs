using ComplianceService.Domain.ApplicationProfile.Entities;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ComplianceService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for EnvironmentConfig entity
/// </summary>
public class EnvironmentConfigConfiguration : IEntityTypeConfiguration<EnvironmentConfig>
{
    public void Configure(EntityTypeBuilder<EnvironmentConfig> builder)
    {
        builder.ToTable("EnvironmentConfigs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EnvironmentName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.IsActive)
            .IsRequired();

        // Configure RiskTier as owned value object stored as string
        builder.Property(e => e.RiskTier)
            .HasConversion(
                v => v.Value,
                v => RiskTier.FromString(v).Value)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("RiskTier");

        // Store SecurityTools as JSON array
        builder.Property<string>("_securityToolsJson")
            .HasColumnName("SecurityTools")
            .IsRequired();

        builder.Ignore(e => e.SecurityTools);

        // Store PolicyReferences as JSON array
        builder.Property<string>("_policyReferencesJson")
            .HasColumnName("PolicyReferences")
            .IsRequired();

        builder.Ignore(e => e.PolicyReferences);

        // Store Metadata as JSON
        builder.Property(e => e.Metadata)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null!)
                     ?? new Dictionary<string, string>())
            .HasColumnName("Metadata");

        // Index for fast lookups by application and environment
        builder.HasIndex("ApplicationId", "EnvironmentName")
            .IsUnique();
    }
}
