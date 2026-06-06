using Seed.Domain.Enums;

namespace Seed.Domain.Entities;

public class InvoiceRequest
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? UserSubscriptionId { get; set; }
    public string? StripeInvoiceId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? ServiceName { get; set; }
    public DateTime? ServicePeriodStart { get; set; }
    public DateTime? ServicePeriodEnd { get; set; }
    public DateTime? InvoicePeriodStart { get; set; }
    public DateTime? InvoicePeriodEnd { get; set; }
    public decimal? AmountSubtotal { get; set; }
    public decimal? AmountTax { get; set; }
    public decimal? AmountTotal { get; set; }
    public decimal? AmountPaid { get; set; }
    public decimal? ProrationAmount { get; set; }
    public bool? IsProrationApplied { get; set; }
    public string? Currency { get; set; }
    public string? BillingReason { get; set; }
    public CustomerType CustomerType { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? FiscalCode { get; set; }
    public string? VatNumber { get; set; }
    public string? SdiCode { get; set; }
    public string? PecEmail { get; set; }
    public InvoiceRequestStatus Status { get; set; } = InvoiceRequestStatus.Requested;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public ApplicationUser? User { get; set; }
    public UserSubscription? UserSubscription { get; set; }
}
