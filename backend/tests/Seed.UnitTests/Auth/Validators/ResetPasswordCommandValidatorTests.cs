using FluentAssertions;
using Seed.Application.Auth.Commands.ResetPassword;

namespace Seed.UnitTests.Auth.Validators;

public class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    [Fact]
    public void Should_Fail_When_Email_Is_Empty()
    {
        var result = _validator.Validate(new ResetPasswordCommand("", "token", "Password1"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Token_Is_Empty()
    {
        var result = _validator.Validate(new ResetPasswordCommand("user@test.com", "", "Password1"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Password_Is_Too_Short()
    {
        var result = _validator.Validate(new ResetPasswordCommand("user@test.com", "token", "Pass1"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Password_Has_No_Uppercase()
    {
        var result = _validator.Validate(new ResetPasswordCommand("user@test.com", "token", "password1"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Password_Has_No_Lowercase()
    {
        var result = _validator.Validate(new ResetPasswordCommand("user@test.com", "token", "PASSWORD1"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Password_Has_No_Digit()
    {
        var result = _validator.Validate(new ResetPasswordCommand("user@test.com", "token", "Passwordd"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_When_All_Fields_Are_Valid()
    {
        var result = _validator.Validate(new ResetPasswordCommand("user@test.com", "token", "Password1"));
        result.IsValid.Should().BeTrue();
    }
}
