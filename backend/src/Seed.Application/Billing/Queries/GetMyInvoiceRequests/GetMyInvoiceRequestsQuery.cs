using MediatR;
using Seed.Application.Billing.Models;
using Seed.Application.Common;

namespace Seed.Application.Billing.Queries.GetMyInvoiceRequests;

public sealed record GetMyInvoiceRequestsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<InvoiceRequestDto>>>;
