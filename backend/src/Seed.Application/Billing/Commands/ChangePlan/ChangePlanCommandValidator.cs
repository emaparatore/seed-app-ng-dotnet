using FluentValidation;

namespace Seed.Application.Billing.Commands.ChangePlan;

public sealed class ChangePlanCommandValidator : AbstractValidator<ChangePlanCommand>
{
    public ChangePlanCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty();

        RuleFor(x => x.BillingInterval)
            .IsInEnum();
    }
}
