using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.SaveQuestion;

internal sealed class SaveQuestionHandler(IAppDbContext db, AccessRules access, ICurrentUser me) : ICommandHandler<SaveQuestionCommand, QuestionDto>
{
    public async Task<Result<QuestionDto>> Handle(SaveQuestionCommand c, CancellationToken ct)
    {
        var input = c.Question;
        if (!await db.Subjects.AnyAsync(s => s.Id == input.SubjectId, ct) || !await db.ClassLevels.AnyAsync(l => l.Id == input.ClassLevelId, ct))
            return CbtErrors.SubjectOrLevel;
        if (!await access.CanAuthorAsync(input.ClassLevelId, input.SubjectId, ct)) return CbtErrors.NotYourSubject;

        Question question;
        if (c.Id is { } id)
        {
            var found = await db.Questions.FirstOrDefaultAsync(x => x.Id == id && !x.IsArchived, ct);
            // Editing needs access to where the question is now as well as where it's going.
            if (found is null || !await access.CanAuthorAsync(found.ClassLevelId, found.SubjectId, ct)) return CbtErrors.QuestionNotFound;
            question = found;
        }
        else
        {
            question = new Question { Text = "", CreatedByUserId = me.UserId };
            db.Questions.Add(question);
        }
        question.Apply(input);
        if (question.Normalise() is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return await db.QuestionDtos(db.Questions.AsNoTracking().Where(x => x.Id == question.Id)).FirstAsync(ct);
    }
}
