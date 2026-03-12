using FluentAssertions;
using Seed.Application.Auth.Commands.ForgotPassword;

namespace Seed.UnitTests.Auth.Validators;

public class ForgotPasswordCommandValidatorTests
{
    private readonly ForgotPasswordCommandValidator _validator = new();

    [Fact]
    public void Should_Fail_When_Email_Is_Empty()
    {
        var result = _validator.Validate(new ForgotPasswordCommand(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_Email_Is_Invalid()
    {
        var result = _validator.Validate(new ForgotPasswordCommand("not-an-email"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_When_Email_Is_Valid()
    {
        var result = _validator.Validate(new ForgotPasswordCommand("user@test.com"));
        result.IsValid.Should().BeTrue();
    }
}
