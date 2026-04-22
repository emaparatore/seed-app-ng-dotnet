using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Persistence.Configurations;

public sealed class InvoiceRequestConfiguration : IEntityTypeConfiguration<InvoiceRequest>
{
    public void Configure(EntityTypeBuilder<InvoiceRequest> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.CustomerType).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.FullName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.CompanyName).HasMaxLength(200);
        builder.Property(i => i.Address).HasMaxLength(500).IsRequired();
        builder.Property(i => i.City).HasMaxLength(100).IsRequired();
        builder.Property(i => i.PostalCode).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Country).HasMaxLength(100).IsRequired();
        builder.Property(i => i.FiscalCode).HasMaxLength(20);
        builder.Property(i => i.VatNumber).HasMaxLength(20);
        builder.Property(i => i.SdiCode).HasMaxLength(10);
        builder.Property(i => i.PecEmail).HasMaxLength(200);
        builder.Property(i => i.StripeInvoiceId).HasMaxLength(100);
        builder.Property(i => i.StripePaymentIntentId).HasMaxLength(100);
        builder.Property(i => i.ServiceName).HasMaxLength(200);
        builder.Property(i => i.Currency).HasMaxLength(10);
        builder.Property(i => i.BillingReason).HasMaxLength(50);
        builder.Property(i => i.AmountSubtotal).HasPrecision(18, 2);
        builder.Property(i => i.AmountTax).HasPrecision(18, 2);
        builder.Property(i => i.AmountTotal).HasPrecision(18, 2);
        builder.Property(i => i.AmountPaid).HasPrecision(18, 2);
        builder.Property(i => i.ProrationAmount).HasPrecision(18, 2);

        builder.HasIndex(i => i.UserId);
        builder.HasIndex(i => i.UserSubscriptionId);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.StripePaymentIntentId);
        builder.HasIndex(i => i.StripeInvoiceId)
            .IsUnique()
            .HasFilter("\"StripeInvoiceId\" IS NOT NULL");

        builder.HasOne(i => i.User)
            .WithMany(u => u.InvoiceRequests)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.UserSubscription)
            .WithMany()
            .HasForeignKey(i => i.UserSubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
