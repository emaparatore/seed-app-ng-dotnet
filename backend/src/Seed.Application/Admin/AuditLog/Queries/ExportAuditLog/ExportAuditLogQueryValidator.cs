using FluentValidation;

namespace Seed.Application.Admin.AuditLog.Queries.ExportAuditLog;

public sealed class ExportAuditLogQueryValidator : AbstractValidator<ExportAuditLogQuery>
{
    public ExportAuditLogQueryValidator()
    {
        RuleFor(x => x.DateFrom)
            .LessThanOrEqualTo(x => x.DateTo)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateFrom must be before or equal to DateTo.");
    }
}
