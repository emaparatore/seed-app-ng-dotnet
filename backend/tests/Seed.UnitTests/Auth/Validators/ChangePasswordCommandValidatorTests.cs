using FluentAssertions;
using Seed.Application.Auth.Commands.ChangePassword;

namespace Seed.UnitTests.Auth.Validators;

public class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    private static ChangePasswordCommand ValidCommand => new(
        Guid.NewGuid().ToString(), "OldPassword1", "NewPassword1");

    [Fact]
    public async Task Should_Pass_When_All_Fields_Are_Valid()
    {
        var result = await _validator.ValidateAsync(ValidCommand);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_CurrentPassword_Is_Empty(string? currentPassword)
    {
        var command = ValidCommand with { CurrentPassword = currentPassword! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CurrentPassword");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_NewPassword_Is_Empty(string? newPassword)
    {
        var command = ValidCommand with { NewPassword = newPassword! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public async Task Should_Fail_When_NewPassword_Is_Too_Short()
    {
        var command = ValidCommand with { NewPassword = "Pass1" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public async Task Should_Fail_When_NewPassword_Has_No_Uppercase()
    {
        var command = ValidCommand with { NewPassword = "password1" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public async Task Should_Fail_When_NewPassword_Has_No_Lowercase()
    {
        var command = ValidCommand with { NewPassword = "PASSWORD1" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public async Task Should_Fail_When_NewPassword_Has_No_Digit()
    {
        var command = ValidCommand with { NewPassword = "Passwordd" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public async Task Should_Fail_When_NewPassword_Equals_CurrentPassword()
    {
        var command = ValidCommand with { CurrentPassword = "Password1", NewPassword = "Password1" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword"
            && e.ErrorMessage.Contains("different"));
    }
}
