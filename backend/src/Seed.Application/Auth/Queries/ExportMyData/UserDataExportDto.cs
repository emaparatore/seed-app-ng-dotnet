namespace Seed.Application.Auth.Queries.ExportMyData;

public sealed record UserDataExportDto(
    UserProfileExportDto Profile,
    UserConsentExportDto Consent,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AuditLogExportDto> AuditLog);

public sealed record UserProfileExportDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsActive);

public sealed record UserConsentExportDto(
    DateTime? PrivacyPolicyAcceptedAt,
    DateTime? TermsAcceptedAt,
    string? ConsentVersion);

public sealed record AuditLogExportDto(
    DateTime Timestamp,
    string Action,
    string EntityType,
    string? EntityId,
    string? Details,
    string? IpAddress);
