using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Queries.ExportMyData;

public sealed record ExportMyDataQuery(Guid UserId) : IRequest<Result<UserDataExportDto>>;
