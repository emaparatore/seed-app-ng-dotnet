using FluentAssertions;
using Seed.Application.Admin.Plans.Commands.UpdatePlan;

namespace Seed.UnitTests.Billing.Commands;

public class UpdatePlanCommandValidatorTests
{
    private readonly UpdatePlanCommandValidator _validator = new();

    private static UpdatePlanCommand ValidCommand() => new(
        Name: "Updated Plan",
        Description: "Description",
        MonthlyPrice: 19.99m,
        YearlyPrice: 199.99m,
        TrialDays: 7,
        IsFreeTier: false,
        IsDefault: false,
        IsPopular: false,
        SortOrder: 1,
        Features: [])
    {
        PlanId = Guid.NewGuid()
    };

    [Fact]
    public void Should_Pass_For_Valid_Command()
    {
        var result = _validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Fail_When_PlanId_Is_Empty()
    {
        var command = ValidCommand() with { PlanId = Guid.Empty };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PlanId");
    }

    [Fact]
    public void Should_Fail_When_Name_Is_Empty()
    {
        var command = ValidCommand() with { Name = "" };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }
}
