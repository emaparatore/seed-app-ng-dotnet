using FluentValidation;

namespace Seed.Application.Admin.Plans.Commands.UpdatePlan;

public sealed class UpdatePlanCommandValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.MonthlyPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.YearlyPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Features)
            .NotNull();
    }
}
