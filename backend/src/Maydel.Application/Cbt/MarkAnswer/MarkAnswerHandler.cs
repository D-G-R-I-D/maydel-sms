using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.MarkAnswer;

internal sealed class MarkAnswerHandler(IAppDbContext db, Tests tests, Attempts attempts, Results results, ICurrentUser me) : ICommandHandler<MarkAnswerCommand, AttemptReview>
{
    public async Task<Result<AttemptReview>> Handle(MarkAnswerCommand c, CancellationToken ct)
    {
        var attempt = await db.TestAttempts.FirstOrDefaultAsync(a => a.Id == c.AttemptId, ct);
        if (attempt is null || await tests.ManageableAsync(attempt.TestId, ct, track: false) is not { } test) return CbtErrors.AttemptNotFound;
        if (test.ResultsReleasedAt is not null) return Error.Rule("test.released", "The results are released, so marks are final.");
        await attempts.CloseIfOverdueAsync(attempt, ct);
        if (attempt.Mark(c.ItemId, c.Mark.Score, c.Mark.Comment, me.UserId, attempts.Now) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return await results.ReviewAsync(attempt, test, ct);
    }
}
