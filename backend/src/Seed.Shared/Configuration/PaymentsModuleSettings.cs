namespace Seed.Shared.Configuration;

public sealed class PaymentsModuleSettings
{
    public bool Enabled { get; init; }
    public string Provider { get; init; } = string.Empty;
}
