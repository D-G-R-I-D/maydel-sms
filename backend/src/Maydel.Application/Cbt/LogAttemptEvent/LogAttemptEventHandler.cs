using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;

namespace Maydel.Application.Cbt.LogAttemptEvent;

internal sealed class LogAttemptEventHandler(IAppDbContext db, Attempts attempts) : ICommandHandler<LogAttemptEventCommand>
{
    public async Task<Result> Handle(LogAttemptEventCommand c, CancellationToken ct)
    {
        if (await attempts.MeAsync(ct) is not { } student) return CbtErrors.NotAStudent;
        if (await attempts.MineAsync(c.AttemptId, student, ct) is not { } attempt) return CbtErrors.AttemptNotFound;
        if (c.Event.Kind is not (IntegrityEventKind.Blur or IntegrityEventKind.Focus)) return Error.Validation("kind", "Send Blur or Focus.");
        if (attempt.Log(c.Event.Kind, c.Event.SessionToken, attempts.Now) is { } invalid)
            return invalid.Code == "attempt.other_session" ? Error.Conflict(invalid.Code, invalid.Message) : invalid.ToError();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
