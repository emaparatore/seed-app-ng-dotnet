using FluentAssertions;
using Seed.Application.Admin.Settings.Commands.UpdateSystemSettings;
using Seed.Application.Admin.Settings.Models;

namespace Seed.UnitTests.Admin.Settings;

public class UpdateSystemSettingsCommandValidatorTests
{
    private readonly UpdateSystemSettingsCommandValidator _validator = new();

    [Fact]
    public async Task Should_Fail_When_Items_Is_Empty()
    {
        var command = new UpdateSystemSettingsCommand([]);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Fact]
    public async Task Should_Fail_When_Key_Is_Empty()
    {
        var command = new UpdateSystemSettingsCommand([new UpdateSettingItem("", "value")]);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Key"));
    }

    [Fact]
    public async Task Should_Fail_When_Value_Is_Null()
    {
        var command = new UpdateSystemSettingsCommand([new UpdateSettingItem("Key", null!)]);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Value"));
    }

    [Fact]
    public async Task Should_Pass_With_Valid_Items()
    {
        var command = new UpdateSystemSettingsCommand(
        [
            new UpdateSettingItem("Security.MaxLoginAttempts", "10"),
            new UpdateSettingItem("General.AppName", "My App")
        ]);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }
}
