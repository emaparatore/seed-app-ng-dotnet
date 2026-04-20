using FluentAssertions;
using Seed.Application.Admin.Plans.Commands.CreatePlan;
using Seed.Application.Admin.Plans.Models;

namespace Seed.UnitTests.Billing.Commands;

public class CreatePlanCommandValidatorTests
{
    private readonly CreatePlanCommandValidator _validator = new();

    private static CreatePlanCommand ValidCommand() => new(
        Name: "Pro Plan",
        Description: "Description",
        MonthlyPrice: 9.99m,
        YearlyPrice: 99.99m,
        TrialDays: 14,
        IsFreeTier: false,
        IsDefault: false,
        IsPopular: false,
        SortOrder: 1,
        Features: [new CreatePlanFeatureRequest("storage", "10 GB", "10", 1)]);

    [Fact]
    public void Should_Pass_For_Valid_Command()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Fail_When_Name_Is_Empty()
    {
        var command = ValidCommand() with { Name = "" };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Should_Fail_When_Name_Is_Too_Long()
    {
        var command = ValidCommand() with { Name = new string('a', 201) };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Should_Fail_When_Price_Is_Negative()
    {
        var command = ValidCommand() with { MonthlyPrice = -1 };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MonthlyPrice");
    }
}
