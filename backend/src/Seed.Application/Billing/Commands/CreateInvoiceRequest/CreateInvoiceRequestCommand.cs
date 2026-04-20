using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;
using Seed.Domain.Enums;

namespace Seed.Application.Billing.Commands.CreateInvoiceRequest;

public sealed record CreateInvoiceRequestCommand(
    CustomerType CustomerType,
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
    string? StripePaymentIntentId) : IRequest<Result<Guid>>
{
    [JsonIgnore]
    public Guid UserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
