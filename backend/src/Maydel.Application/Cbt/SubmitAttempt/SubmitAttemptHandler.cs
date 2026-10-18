using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;

namespace Maydel.Application.Cbt.SubmitAttempt;

internal sealed class SubmitAttemptHandler(IAppDbContext db, Attempts attempts) : ICommandHandler<SubmitAttemptCommand, AttemptPaper>
{
    public async Task<Result<AttemptPaper>> Handle(SubmitAttemptCommand c, CancellationToken ct)
    {
        if (await attempts.MeAsync(ct) is not { } student) return CbtErrors.NotAStudent;
        if (await attempts.MineAsync(c.AttemptId, student, ct) is not { } attempt) return CbtErrors.AttemptNotFound;
        if (attempt.Status == AttemptStatus.Submitted) return await attempts.PaperAsync(attempt, includeToken: false, ct); // time ran out first: already in
        if (attempt.Submit(c.SessionToken, attempts.Now, await attempts.QuestionsAsync(attempt, ct)) is { } invalid)
            return invalid.Code == "attempt.other_session" ? Error.Conflict(invalid.Code, invalid.Message) : invalid.ToError();
        await db.SaveChangesAsync(ct);
        return await attempts.PaperAsync(attempt, includeToken: false, ct);
    }
}
