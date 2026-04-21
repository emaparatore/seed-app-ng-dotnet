using FluentValidation;

namespace Seed.Application.Admin.InvoiceRequests.Commands.UpdateInvoiceRequestStatus;

public sealed class UpdateInvoiceRequestStatusCommandValidator : AbstractValidator<UpdateInvoiceRequestStatusCommand>
{
    public UpdateInvoiceRequestStatusCommandValidator()
    {
        RuleFor(x => x.InvoiceRequestId)
            .NotEmpty();

        RuleFor(x => x.NewStatus)
            .IsInEnum();
    }
}
