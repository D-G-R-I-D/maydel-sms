using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.SetQuestionImage;

internal sealed class SetQuestionImageHandler(IAppDbContext db, AccessRules access, PhotoStore photos) : ICommandHandler<SetQuestionImageCommand, QuestionDto>
{
    public async Task<Result<QuestionDto>> Handle(SetQuestionImageCommand c, CancellationToken ct)
    {
        var question = await db.Questions.FirstOrDefaultAsync(x => x.Id == c.Id && !x.IsArchived, ct);
        if (question is null || !await access.CanAuthorAsync(question.ClassLevelId, question.SubjectId, ct)) return CbtErrors.QuestionNotFound;

        var old = question.ImagePath;
        if (c.File is { } file)
        {
            // PNG keeps diagrams and line drawings crisp.
            var saved = await photos.SaveAsync(file, "questions", transparent: true, 1200, ct);
            if (saved.IsFailure) return saved.Error!;
            question.ImagePath = saved.Value.Key;
        }
        else
        {
            question.ImagePath = null;
        }
        await db.SaveChangesAsync(ct);
        if (old is not null && old != question.ImagePath) await photos.DeleteQuietlyAsync(old, ct);
        return await db.QuestionDtos(db.Questions.AsNoTracking().Where(x => x.Id == question.Id)).FirstAsync(ct);
    }
}
