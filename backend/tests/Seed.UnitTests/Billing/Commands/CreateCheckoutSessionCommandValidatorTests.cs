using FluentAssertions;
using Seed.Application.Billing.Commands.CreateCheckoutSession;
using Seed.Domain.Enums;

namespace Seed.UnitTests.Billing.Commands;

public class CreateCheckoutSessionCommandValidatorTests
{
    private readonly CreateCheckoutSessionCommandValidator _validator = new();

    private static CreateCheckoutSessionCommand ValidCommand => new(
        Guid.NewGuid(),
        BillingInterval.Monthly,
        "https://example.com/success",
        "https://example.com/cancel");

    [Fact]
    public async Task Should_Pass_When_All_Fields_Are_Valid()
    {
        var result = await _validator.ValidateAsync(ValidCommand);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Fail_When_PlanId_Is_Empty()
    {
        var command = ValidCommand with { PlanId = Guid.Empty };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PlanId");
    }

    [Fact]
    public async Task Should_Fail_When_SuccessUrl_Is_Empty()
    {
        var command = ValidCommand with { SuccessUrl = "" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SuccessUrl");
    }

    [Fact]
    public async Task Should_Fail_When_CancelUrl_Is_Empty()
    {
        var command = ValidCommand with { CancelUrl = "" };
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CancelUrl");
    }
}
