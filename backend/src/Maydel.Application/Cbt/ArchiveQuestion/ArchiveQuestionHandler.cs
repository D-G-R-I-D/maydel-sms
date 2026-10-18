using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.ArchiveQuestion;

internal sealed class ArchiveQuestionHandler(IAppDbContext db, AccessRules access) : ICommandHandler<ArchiveQuestionCommand>
{
    public async Task<Result> Handle(ArchiveQuestionCommand c, CancellationToken ct)
    {
        var question = await db.Questions.FirstOrDefaultAsync(x => x.Id == c.Id && !x.IsArchived, ct);
        if (question is null || !await access.CanAuthorAsync(question.ClassLevelId, question.SubjectId, ct)) return CbtErrors.QuestionNotFound;
        question.IsArchived = true;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
