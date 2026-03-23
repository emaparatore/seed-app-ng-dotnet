using FluentValidation;

namespace Seed.Application.Admin.Settings.Commands.UpdateSystemSettings;

public sealed class UpdateSystemSettingsCommandValidator : AbstractValidator<UpdateSystemSettingsCommand>
{
    public UpdateSystemSettingsCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one setting must be provided.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Key)
                .NotEmpty()
                .WithMessage("Setting key must not be empty.");

            item.RuleFor(x => x.Value)
                .NotNull()
                .WithMessage("Setting value must not be null.");
        });
    }
}
