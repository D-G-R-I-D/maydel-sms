using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.UpdateTerm;

internal sealed class UpdateTermHandler(IAppDbContext db) : ICommandHandler<UpdateTermCommand, TermDto>
{
    public async Task<Result<TermDto>> Handle(UpdateTermCommand c, CancellationToken ct)
    {
        var sessionId = await db.Terms.Where(t => t.Id == c.Id).Select(t => (Guid?)t.SessionId).FirstOrDefaultAsync(ct);
        if (sessionId is null) return AcademicsErrors.TermNotFound;
        var session = await db.Sessions.Include(s => s.Terms).SingleAsync(s => s.Id == sessionId, ct);
        var term = session.Terms.Single(t => t.Id == c.Id);

        if (session.RescheduleTerm(term, c.StartsOn, c.EndsOn, c.MidTermStartsOn, c.MidTermEndsOn) is { } invalid) return invalid.ToError();

        await db.SaveChangesAsync(ct);
        return await db.Terms.AsNoTracking().Where(t => t.Id == term.Id).ToDtos().SingleAsync(ct);
    }
}
