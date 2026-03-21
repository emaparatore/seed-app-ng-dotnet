namespace Seed.Shared.Configuration;

public sealed class SuperAdminSettings
{
    public const string SectionName = "SuperAdmin";
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FirstName { get; init; } = "Super";
    public string LastName { get; init; } = "Admin";
}
