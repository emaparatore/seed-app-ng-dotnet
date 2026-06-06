using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Persistence.Configurations;

public sealed class CheckoutSessionAttemptConfiguration : IEntityTypeConfiguration<CheckoutSessionAttempt>
{
    public void Configure(EntityTypeBuilder<CheckoutSessionAttempt> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.StripeSessionId).HasMaxLength(100);
        builder.Property(x => x.StripeSubscriptionId).HasMaxLength(100);
        builder.Property(x => x.StripeCustomerId).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });
        builder.HasIndex(x => x.StripeSessionId)
            .IsUnique()
            .HasFilter("\"StripeSessionId\" IS NOT NULL");
        builder.HasIndex(x => x.StripeSubscriptionId)
            .HasFilter("\"StripeSubscriptionId\" IS NOT NULL");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
