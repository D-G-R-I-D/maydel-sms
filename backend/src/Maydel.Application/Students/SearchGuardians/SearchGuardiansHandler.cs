using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.SearchGuardians;

internal sealed class SearchGuardiansHandler(IAppDbContext db) : IQueryHandler<SearchGuardiansQuery, PagedResult<GuardianListItem>>
{
    public async Task<Result<PagedResult<GuardianListItem>>> Handle(SearchGuardiansQuery q, CancellationToken ct)
    {
        var guardians = db.Guardians.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLower();
            var phone = Guardian.NormalizePhone(q.Search);
            guardians = guardians.Where(g => g.FullName.ToLower().Contains(term) || (g.Email != null && g.Email.Contains(term))
                || g.Phone.Contains(term) || (phone != null && g.Phone == phone));
        }

        return await guardians.OrderBy(g => g.FullName)
            .Select(g => new GuardianListItem(g.Id, g.FullName, g.Phone, g.Email, g.Students.Count, g.UserId != null))
            .ToPagedAsync(q.Page, q.PageSize, ct);
    }
}
