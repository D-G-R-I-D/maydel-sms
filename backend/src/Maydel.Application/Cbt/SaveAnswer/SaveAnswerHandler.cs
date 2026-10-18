using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;

namespace Maydel.Application.Cbt.SaveAnswer;

internal sealed class SaveAnswerHandler(IAppDbContext db, Attempts attempts) : ICommandHandler<SaveAnswerCommand, AnswerSaved>
{
    public async Task<Result<AnswerSaved>> Handle(SaveAnswerCommand c, CancellationToken ct)
    {
        if (await attempts.MeAsync(ct) is not { } student) return CbtErrors.NotAStudent;
        if (await attempts.MineAsync(c.AttemptId, student, ct) is not { } attempt) return CbtErrors.AttemptNotFound;
        var now = attempts.Now;
        var a = c.Answer;
        if (attempt.Answer(c.ItemId, new QuestionAnswer(a.OptionIds, a.TrueFalse, a.Text), a.SessionToken, now) is { } invalid)
            return invalid.Code == "attempt.other_session" ? Error.Conflict(invalid.Code, invalid.Message) : invalid.ToError();
        await db.SaveChangesAsync(ct);
        return new AnswerSaved(c.ItemId, now, Math.Max(0, (int)(attempt.DeadlineAt - now).TotalSeconds));
    }
}
