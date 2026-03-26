namespace Seed.Application.Auth.Commands.ChangePassword;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
