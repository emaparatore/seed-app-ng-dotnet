using FluentValidation;
using Seed.Domain.Enums;

namespace Seed.Application.Billing.Commands.CreateCheckoutSession;

public sealed class CreateCheckoutSessionCommandValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty();

        RuleFor(x => x.BillingInterval)
            .IsInEnum();

        RuleFor(x => x.SuccessUrl)
            .NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("SuccessUrl must be a valid absolute URL.");

        RuleFor(x => x.CancelUrl)
            .NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("CancelUrl must be a valid absolute URL.");
    }
}
