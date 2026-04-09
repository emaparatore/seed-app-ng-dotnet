namespace Seed.Shared.Configuration;

public sealed class DataRetentionSettings
{
    public const string SectionName = "DataRetention";

    public int SoftDeletedUserRetentionDays { get; init; } = 30;
    public int RefreshTokenRetentionDays { get; init; } = 7;
    public int AuditLogRetentionDays { get; init; } = 365;
    public int CleanupIntervalHours { get; init; } = 24;
}
