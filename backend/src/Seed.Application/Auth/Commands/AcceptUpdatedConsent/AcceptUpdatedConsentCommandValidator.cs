using FluentValidation;

namespace Seed.Application.Auth.Commands.AcceptUpdatedConsent;

public sealed class AcceptUpdatedConsentCommandValidator : AbstractValidator<AcceptUpdatedConsentCommand>
{
    public AcceptUpdatedConsentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
