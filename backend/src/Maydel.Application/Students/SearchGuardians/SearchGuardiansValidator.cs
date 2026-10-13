using FluentValidation;
using Maydel.Application.Common;

namespace Maydel.Application.Students.SearchGuardians;

internal sealed class SearchGuardiansValidator : AbstractValidator<SearchGuardiansQuery>
{
    public SearchGuardiansValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}
