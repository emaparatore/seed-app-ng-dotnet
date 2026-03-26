namespace Seed.Application.Admin.Settings.Models;

public sealed record SystemSettingDto(
    string Key,
    string Value,
    string Type,
    string Category,
    string? Description,
    Guid? ModifiedBy,
    DateTime? ModifiedAt);
