namespace Seed.Shared.Configuration;

public sealed class ModulesSettings
{
    public const string SectionName = "Modules";

    public PaymentsModuleSettings Payments { get; init; } = new();
}
