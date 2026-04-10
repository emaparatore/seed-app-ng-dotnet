namespace Seed.Domain.Authorization;

public static class AuditActions
{
    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
    public const string UserDeleted = "UserDeleted";
    public const string UserStatusChanged = "UserStatusChanged";
    public const string UserRolesChanged = "UserRolesChanged";

    public const string RoleCreated = "RoleCreated";
    public const string RoleUpdated = "RoleUpdated";
    public const string RoleDeleted = "RoleDeleted";

    public const string LoginSuccess = "LoginSuccess";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordReset = "PasswordReset";
    public const string PasswordResetRequested = "PasswordResetRequested";

    public const string SettingsChanged = "SettingsChanged";
    public const string SystemSeeding = "SystemSeeding";

    public const string AccountDeleted = "AccountDeleted";
    public const string EmailConfirmed = "EmailConfirmed";
    public const string ConfirmationEmailResent = "ConfirmationEmailResent";
    public const string ConsentGiven = "ConsentGiven";
    public const string DataExported = "DataExported";

    // Subscription & Payments
    public const string SubscriptionCreated = "SubscriptionCreated";
    public const string SubscriptionUpdated = "SubscriptionUpdated";
    public const string SubscriptionCanceled = "SubscriptionCanceled";
    public const string SubscriptionPaymentSucceeded = "SubscriptionPaymentSucceeded";
    public const string SubscriptionPaymentFailed = "SubscriptionPaymentFailed";
    public const string WebhookReceived = "WebhookReceived";
    public const string WebhookVerificationFailed = "WebhookVerificationFailed";
}
