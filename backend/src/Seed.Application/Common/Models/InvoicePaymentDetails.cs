namespace Seed.Application.Common.Models;

public sealed record InvoicePaymentDetails(
    string StripeInvoiceId,
    string? StripePaymentIntentId,
    DateTime? InvoicePeriodStart,
    DateTime? InvoicePeriodEnd,
    string? Currency,
    decimal AmountSubtotal,
    decimal AmountTax,
    decimal AmountTotal,
    decimal AmountPaid,
    bool IsProrationApplied,
    decimal ProrationAmount,
    string? BillingReason);
