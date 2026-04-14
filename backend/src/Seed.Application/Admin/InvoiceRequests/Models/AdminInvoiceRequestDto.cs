namespace Seed.Application.Admin.InvoiceRequests.Models;

public sealed record AdminInvoiceRequestDto(
    Guid Id,
    string UserEmail,
    string UserFullName,
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
