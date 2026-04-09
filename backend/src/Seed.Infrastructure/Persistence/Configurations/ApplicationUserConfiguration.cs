using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.MustChangePassword).HasDefaultValue(false);
        builder.Property(u => u.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(u => !u.IsDeleted);

        builder.Property(u => u.PrivacyPolicyAcceptedAt).IsRequired(false);
        builder.Property(u => u.TermsAcceptedAt).IsRequired(false);
        builder.Property(u => u.ConsentVersion).HasMaxLength(20).IsRequired(false);
    }
}
