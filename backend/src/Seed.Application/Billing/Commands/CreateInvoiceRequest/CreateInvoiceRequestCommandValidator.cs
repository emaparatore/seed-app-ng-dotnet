using FluentValidation;
using Seed.Domain.Enums;

namespace Seed.Application.Billing.Commands.CreateInvoiceRequest;

public sealed class CreateInvoiceRequestCommandValidator : AbstractValidator<CreateInvoiceRequestCommand>
{
    public CreateInvoiceRequestCommandValidator()
    {
        RuleFor(x => x.CustomerType)
            .IsInEnum();

        RuleFor(x => x.FullName)
            .NotEmpty();

        RuleFor(x => x.Address)
            .NotEmpty();

        RuleFor(x => x.City)
            .NotEmpty();

        RuleFor(x => x.PostalCode)
            .NotEmpty();

        RuleFor(x => x.Country)
            .NotEmpty();

        When(x => x.CustomerType == CustomerType.Company, () =>
        {
            RuleFor(x => x.CompanyName)
                .NotEmpty();

            RuleFor(x => x.VatNumber)
                .NotEmpty();
        });
    }
}
