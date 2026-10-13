using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.MakeTermCurrent;

internal sealed class MakeTermCurrentHandler(IAppDbContext db) : ICommandHandler<MakeTermCurrentCommand, TermDto>
{
    public async Task<Result<TermDto>> Handle(MakeTermCurrentCommand c, CancellationToken ct)
    {
        var term = await db.Terms.Include(t => t.Session).FirstOrDefaultAsync(t => t.Id == c.Id, ct);
        if (term is null) return AcademicsErrors.TermNotFound;

        // Tracked updates (not ExecuteUpdate) so every flag change lands in the audit log.
        foreach (var other in await db.Terms.Where(t => t.IsCurrent && t.Id != term.Id).ToListAsync(ct)) other.IsCurrent = false;
        foreach (var other in await db.Sessions.Where(s => s.IsCurrent && s.Id != term.SessionId).ToListAsync(ct)) other.IsCurrent = false;
        term.IsCurrent = true;
        term.Session.IsCurrent = true;

        await db.SaveChangesAsync(ct);
        return await db.Terms.AsNoTracking().Where(t => t.Id == term.Id).ToDtos().SingleAsync(ct);
    }
}
