using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.UpdateSession;

internal sealed class UpdateSessionHandler(IAppDbContext db) : ICommandHandler<UpdateSessionCommand, SessionDto>
{
    public async Task<Result<SessionDto>> Handle(UpdateSessionCommand command, CancellationToken ct)
    {
        var session = await db.Sessions.Include(s => s.Terms).FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (session is null) return AcademicsErrors.SessionNotFound;

        var name = command.Name.Trim();
        if (await db.Sessions.AnyAsync(s => s.Name == name && s.Id != command.Id, ct)) return AcademicsErrors.DuplicateSession(name);
        if (session.Reschedule(name, command.StartsOn, command.EndsOn) is { } invalid) return invalid.ToError();

        await db.SaveChangesAsync(ct);
        return (await db.GetSessionAsync(session.Id, ct))!;
    }
}
