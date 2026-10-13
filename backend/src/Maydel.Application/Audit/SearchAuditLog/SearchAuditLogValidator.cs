using FluentValidation;
using Maydel.Application.Common;

namespace Maydel.Application.Audit.SearchAuditLog;

internal sealed class SearchAuditLogValidator : AbstractValidator<SearchAuditLogQuery>
{
    public SearchAuditLogValidator()
    {
        RuleFor(x => x.EntityType).MaximumLength(256);
        RuleFor(x => x.EntityId).MaximumLength(256);
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("'To' must be after 'From'.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}
