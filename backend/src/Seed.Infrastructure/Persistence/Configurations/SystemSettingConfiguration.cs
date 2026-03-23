using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.HasKey(e => e.Key);

        builder.Property(e => e.Key).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.HasIndex(e => e.Category);
    }
}
