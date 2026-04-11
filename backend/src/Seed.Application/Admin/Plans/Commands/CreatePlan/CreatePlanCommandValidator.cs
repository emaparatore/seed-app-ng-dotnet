using FluentValidation;

namespace Seed.Application.Admin.Plans.Commands.CreatePlan;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
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
