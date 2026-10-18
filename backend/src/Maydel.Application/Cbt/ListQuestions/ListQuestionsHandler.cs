using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.ListQuestions;

internal sealed class ListQuestionsHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListQuestionsQuery, PagedResult<QuestionDto>>
{
    public async Task<Result<PagedResult<QuestionDto>>> Handle(ListQuestionsQuery q, CancellationToken ct)
    {
        if (!await access.CanAuthorAsync(q.ClassLevelId, q.SubjectId, ct)) return CbtErrors.NotYourSubject;
        var questions = db.Questions.AsNoTracking().Where(x => x.SubjectId == q.SubjectId && x.ClassLevelId == q.ClassLevelId && !x.IsArchived);
        if (q.Kind is { } kind) questions = questions.Where(x => x.Kind == kind);
        if (q.Difficulty is { } difficulty) questions = questions.Where(x => x.Difficulty == difficulty);
        if (!string.IsNullOrWhiteSpace(q.Topic)) questions = questions.Where(x => x.Topic == q.Topic.Trim());
        foreach (var word in (q.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
        {
            var w = word.ToLower();
            questions = questions.Where(x => x.Text.ToLower().Contains(w) || (x.Topic != null && x.Topic.ToLower().Contains(w)));
        }
        return await db.QuestionDtos(questions.OrderByDescending(x => x.CreatedAt)).ToPagedAsync(q.Page, q.PageSize, ct);
    }
}
