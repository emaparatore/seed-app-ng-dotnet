namespace Seed.Shared.Configuration;

public sealed class ClientSettings
{
    public const string SectionName = "Client";

    public string BaseUrl { get; init; } = "http://localhost:4200";
}
