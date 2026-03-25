namespace Seed.Domain.Authorization;

public sealed record SystemSettingDefault(
    string Key,
    string Value,
    string Type,
    string Category,
    string Description);

public static class SystemSettingsDefaults
{
    public static IReadOnlyList<SystemSettingDefault> GetAll() =>
    [
        new("Security.MaxLoginAttempts", "5", "int", "Security", "Maximum failed login attempts before lockout"),
        new("Security.LockoutDurationMinutes", "15", "int", "Security", "Account lockout duration in minutes"),
        new("Security.PasswordMinLength", "8", "int", "Security", "Minimum password length"),
        new("Email.SendWelcomeEmail", "true", "bool", "Email", "Send welcome email on user registration"),
        new("Email.SendPasswordResetNotification", "true", "bool", "Email", "Send notification after password reset"),
        new("AuditLog.RetentionMonths", "0", "int", "AuditLog", "Audit log retention in months (0 = no retention)"),
        new("General.MaintenanceMode", "false", "bool", "General", "Enable maintenance mode"),
        new("General.AppName", "Seed App", "string", "General", "Application display name")
    ];
}
