namespace Seed.Shared.Configuration;

public sealed class PrivacySettings
{
    public const string SectionName = "Privacy";

    public string ConsentVersion { get; init; } = "1.0";
}
