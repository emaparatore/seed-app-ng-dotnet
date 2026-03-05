using FluentAssertions;
using Seed.Application.Auth.Commands.Register;

namespace Seed.UnitTests.Auth.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand ValidCommand => new(
        "test@example.com", "Password1", "John", "Doe");

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

    [Fact]
    public async Task Should_Fail_When_Password_Is_Too_Short()
    {
        var command = ValidCommand with { Password = "Pass1" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Should_Fail_When_Password_Has_No_Uppercase()
    {
        var command = ValidCommand with { Password = "password1" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Should_Fail_When_Password_Has_No_Lowercase()
    {
        var command = ValidCommand with { Password = "PASSWORD1" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Should_Fail_When_Password_Has_No_Digit()
    {
        var command = ValidCommand with { Password = "Passwordd" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_FirstName_Is_Empty(string? firstName)
    {
        var command = ValidCommand with { FirstName = firstName! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public async Task Should_Fail_When_FirstName_Exceeds_Max_Length()
    {
        var command = ValidCommand with { FirstName = new string('a', 101) };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Fail_When_LastName_Is_Empty(string? lastName)
    {
        var command = ValidCommand with { LastName = lastName! };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
    }

    [Fact]
    public async Task Should_Fail_When_LastName_Exceeds_Max_Length()
    {
        var command = ValidCommand with { LastName = new string('a', 101) };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
    }
}
