using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Persistence.Configurations;

public sealed class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Key).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(500).IsRequired();
        builder.Property(f => f.LimitValue).HasMaxLength(50);

        builder.HasIndex(f => new { f.PlanId, f.Key }).IsUnique();
    }
}
