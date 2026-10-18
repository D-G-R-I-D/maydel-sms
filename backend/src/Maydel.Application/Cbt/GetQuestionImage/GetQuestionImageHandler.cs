using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.GetQuestionImage;

internal sealed class GetQuestionImageHandler(IAppDbContext db, AccessRules access, PhotoStore photos) : IQueryHandler<GetQuestionImageQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetQuestionImageQuery q, CancellationToken ct)
    {
        var question = await db.Questions.AsNoTracking().Where(x => x.Id == q.Id).Select(x => new { x.SubjectId, x.ClassLevelId, x.ImagePath }).FirstOrDefaultAsync(ct);
        if (question is null || !await access.CanAuthorAsync(question.ClassLevelId, question.SubjectId, ct)) return CbtErrors.QuestionNotFound;
        return await photos.OpenAsync(question.ImagePath, ct);
    }
}
