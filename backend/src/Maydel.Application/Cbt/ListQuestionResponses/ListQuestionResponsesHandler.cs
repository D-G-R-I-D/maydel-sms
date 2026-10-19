using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.ListQuestionResponses;

internal sealed class ListQuestionResponsesHandler(IAppDbContext db, Tests tests, Results results) : IQueryHandler<ListQuestionResponsesQuery, QuestionResponses>
{
    public async Task<Result<QuestionResponses>> Handle(ListQuestionResponsesQuery q, CancellationToken ct)
    {
        if (await tests.ManageableAsync(q.TestId, ct, track: false) is not { } test) return CbtErrors.TestNotFound;
        var question = await db.Questions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.QuestionId, ct);
        if (question is null) return CbtErrors.QuestionNotFound;
        var attempts = (await results.AttemptsAsync(test.Id, ct)).Where(a => a.Status == AttemptStatus.Submitted).ToList();
        var ids = attempts.Select(a => a.StudentId).ToList();
        var names = await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.FirstName + " " + s.LastName, ct);
        var responses = attempts
            .SelectMany(a => a.Items.Where(i => i.QuestionId == q.QuestionId).Select(i => new QuestionResponse(a.Id, i.Id, names.GetValueOrDefault(a.StudentId, ""), i.Marks,
                i.AnswerText ?? (i.AnswerTrueFalse is { } tf ? (tf ? "True" : "False") : null), i.AnsweredAt is not null, i.AutoScore, i.Score, i.TeacherComment)))
            .OrderBy(r => r.Score is not null).ThenBy(r => r.StudentName)
            .ToList();
        return new QuestionResponses(question.Id, question.Kind, question.Text, question.MarkingGuide, question.AcceptedAnswers, responses);
    }
}
