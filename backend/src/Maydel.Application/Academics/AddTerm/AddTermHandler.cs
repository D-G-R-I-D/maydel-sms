using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.AddTerm;

internal sealed class AddTermHandler(IAppDbContext db) : ICommandHandler<AddTermCommand, TermDto>
{
    public async Task<Result<TermDto>> Handle(AddTermCommand c, CancellationToken ct)
    {
        var session = await db.Sessions.Include(s => s.Terms).FirstOrDefaultAsync(s => s.Id == c.SessionId, ct);
        if (session is null) return AcademicsErrors.SessionNotFound;

        var term = new Term { Number = c.Number, StartsOn = c.StartsOn, EndsOn = c.EndsOn, MidTermStartsOn = c.MidTermStartsOn, MidTermEndsOn = c.MidTermEndsOn };
        if (session.AddTerm(term) is { } invalid) return invalid.ToError();

        await db.SaveChangesAsync(ct);
        return await db.Terms.AsNoTracking().Where(t => t.Id == term.Id).ToDtos().SingleAsync(ct);
    }
}
