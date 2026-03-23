using FluentValidation;

namespace Seed.Application.Admin.AuditLog.Queries.GetAuditLogEntries;

public sealed class GetAuditLogEntriesQueryValidator : AbstractValidator<GetAuditLogEntriesQuery>
{
    public GetAuditLogEntriesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);
    }
}
