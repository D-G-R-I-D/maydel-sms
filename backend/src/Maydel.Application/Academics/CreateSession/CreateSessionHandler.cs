using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.CreateSession;

internal sealed class CreateSessionHandler(IAppDbContext db) : ICommandHandler<CreateSessionCommand, SessionDto>
{
    public async Task<Result<SessionDto>> Handle(CreateSessionCommand command, CancellationToken ct)
    {
        var name = command.Name.Trim();
        if (AcademicSession.Validate(name, command.StartsOn, command.EndsOn) is { } invalid) return invalid.ToError();
        if (await db.Sessions.AnyAsync(s => s.Name == name, ct)) return AcademicsErrors.DuplicateSession(name);

        var session = new AcademicSession { Name = name, StartsOn = command.StartsOn, EndsOn = command.EndsOn };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);
        return (await db.GetSessionAsync(session.Id, ct))!;
    }
}
