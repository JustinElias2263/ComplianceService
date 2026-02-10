using ComplianceService.Domain.ApplicationProfile;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for Application aggregate root
/// </summary>
public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Owner)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.IsActive)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .IsRequired();

        // Index for fast lookups by name
        builder.HasIndex(a => a.Name)
            .IsUnique();

        // Index for filtering by owner
        builder.HasIndex(a => a.Owner);

        // One-to-many relationship with EnvironmentConfig
        builder.HasMany(a => a.Environments)
            .WithOne()
            .HasForeignKey("ApplicationId")
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events (not persisted)
        builder.Ignore(a => a.DomainEvents);
    }
}
