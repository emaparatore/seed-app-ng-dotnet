namespace Seed.Domain.Authorization;

public static class Permissions
{
    public static class Users
    {
        public const string Read = "Users.Read";
        public const string Create = "Users.Create";
        public const string Update = "Users.Update";
        public const string Delete = "Users.Delete";
        public const string ToggleStatus = "Users.ToggleStatus";
        public const string AssignRoles = "Users.AssignRoles";
    }

    public static class Roles
    {
        public const string Read = "Roles.Read";
        public const string Create = "Roles.Create";
        public const string Update = "Roles.Update";
        public const string Delete = "Roles.Delete";
    }

    public static class AuditLog
    {
        public const string Read = "AuditLog.Read";
        public const string Export = "AuditLog.Export";
    }

    public static class Settings
    {
        public const string Read = "Settings.Read";
        public const string Manage = "Settings.Manage";
    }

    public static class Dashboard
    {
        public const string ViewStats = "Dashboard.ViewStats";
    }

    public static class SystemHealth
    {
        public const string Read = "SystemHealth.Read";
    }

    public static class Plans
    {
        public const string Read = "Plans.Read";
        public const string Create = "Plans.Create";
        public const string Update = "Plans.Update";
    }

    public static class Subscriptions
    {
        public const string Read = "Subscriptions.Read";
        public const string Manage = "Subscriptions.Manage";
    }

    private static readonly string[] All =
    [
        Users.Read, Users.Create, Users.Update, Users.Delete, Users.ToggleStatus, Users.AssignRoles,
        Roles.Read, Roles.Create, Roles.Update, Roles.Delete,
        AuditLog.Read, AuditLog.Export,
        Settings.Read, Settings.Manage,
        Dashboard.ViewStats,
        SystemHealth.Read,
        Plans.Read, Plans.Create, Plans.Update,
        Subscriptions.Read, Subscriptions.Manage
    ];

    public static IReadOnlyList<string> GetAll() => All;
}
