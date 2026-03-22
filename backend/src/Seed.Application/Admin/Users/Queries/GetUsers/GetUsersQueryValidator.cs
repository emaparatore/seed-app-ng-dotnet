using FluentValidation;

namespace Seed.Application.Admin.Users.Queries.GetUsers;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.SortBy)
            .Must(x => x is null or "email" or "firstname" or "lastname" or "isactive" or "createdat")
            .WithMessage("SortBy must be one of: email, firstname, lastname, isactive, createdat");
    }
}
