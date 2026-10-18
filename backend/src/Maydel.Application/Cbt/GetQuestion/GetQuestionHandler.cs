using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.GetQuestion;

internal sealed class GetQuestionHandler(IAppDbContext db, AccessRules access) : IQueryHandler<GetQuestionQuery, QuestionDto>
{
    public async Task<Result<QuestionDto>> Handle(GetQuestionQuery q, CancellationToken ct)
    {
        var scope = await db.Questions.AsNoTracking().Where(x => x.Id == q.Id).Select(x => new { x.SubjectId, x.ClassLevelId }).FirstOrDefaultAsync(ct);
        // Someone outside the subject can't tell whether the question exists.
        if (scope is null || !await access.CanAuthorAsync(scope.ClassLevelId, scope.SubjectId, ct)) return CbtErrors.QuestionNotFound;
        return await db.QuestionDtos(db.Questions.AsNoTracking().Where(x => x.Id == q.Id)).FirstAsync(ct);
    }
}
