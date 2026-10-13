using FluentValidation;
using Maydel.Application.Common;

namespace Maydel.Application.StaffMembers.ListStaff;

internal sealed class ListStaffValidator : AbstractValidator<ListStaffQuery>
{
    public ListStaffValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}
