using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;
using Seed.Domain.Enums;

namespace Seed.Application.Admin.InvoiceRequests.Commands.UpdateInvoiceRequestStatus;

public sealed record UpdateInvoiceRequestStatusCommand(
    InvoiceRequestStatus NewStatus) : IRequest<Result<bool>>
{
    [JsonIgnore]
    public Guid InvoiceRequestId { get; init; }

    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
