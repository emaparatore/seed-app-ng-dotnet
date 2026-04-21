namespace Seed.Application.Billing.Models;

public sealed record InvoiceRequestDto(
    Guid Id,
    string CustomerType,
    string FullName,
    string? CompanyName,
    string Address,
    string City,
    string PostalCode,
    string Country,
    string? FiscalCode,
    string? VatNumber,
    string? SdiCode,
    string? PecEmail,
    string? StripePaymentIntentId,
    string Status,
    DateTime CreatedAt,
    DateTime? ProcessedAt);
