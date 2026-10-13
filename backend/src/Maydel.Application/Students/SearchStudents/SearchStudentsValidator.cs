using FluentValidation;
using Maydel.Application.Common;

namespace Maydel.Application.Students.SearchStudents;

internal sealed class SearchStudentsValidator : AbstractValidator<SearchStudentsQuery>
{
    public SearchStudentsValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}
