using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Admin.Settings.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Settings.Commands.UpdateSystemSettings;

public sealed record UpdateSystemSettingsCommand(
    List<UpdateSettingItem> Items) : IRequest<Result<bool>>
{
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
