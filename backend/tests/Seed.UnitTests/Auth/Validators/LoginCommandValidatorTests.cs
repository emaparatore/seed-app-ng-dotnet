using FluentAssertions;
using Seed.Application.Auth.Commands.Login;

namespace Seed.UnitTests.Auth.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    private static LoginCommand ValidCommand => new("test@example.com", "Password1");

    [Fact]
    public async Task Should_Pass_When_All_Fields_Are_Valid()
    {
        var result = await _validator.ValidateAsync(ValidCommand);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_Email_Is_Empty(string? email)
    {
        var command = ValidCommand with { Email = email! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task Should_Fail_When_Email_Is_Invalid_Format()
    {
        var command = ValidCommand with { Email = "not-an-email" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_Password_Is_Empty(string? password)
    {
        var command = ValidCommand with { Password = password! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}
