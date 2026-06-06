using FluentValidation;

namespace Seed.Application.Billing.Commands.ConfirmCheckoutSession;

public sealed class ConfirmCheckoutSessionCommandValidator : AbstractValidator<ConfirmCheckoutSessionCommand>
{
    public ConfirmCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .MaximumLength(200);
    }
}
