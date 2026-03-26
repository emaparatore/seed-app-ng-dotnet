using MediatR;
using Seed.Application.Admin.Settings.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Settings.Queries.GetSystemSettings;

public sealed record GetSystemSettingsQuery : IRequest<Result<IReadOnlyList<SystemSettingDto>>>;
