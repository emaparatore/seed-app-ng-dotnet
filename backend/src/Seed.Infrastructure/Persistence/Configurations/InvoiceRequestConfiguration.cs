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
        builder.Property(i => i.StripePaymentIntentId).HasMaxLength(100);

        builder.HasIndex(i => i.UserId);
        builder.HasIndex(i => i.Status);

        builder.HasOne(i => i.User)
            .WithMany(u => u.InvoiceRequests)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
