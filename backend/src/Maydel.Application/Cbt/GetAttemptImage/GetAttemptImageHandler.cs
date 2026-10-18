using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.GetAttemptImage;

internal sealed class GetAttemptImageHandler(IAppDbContext db, Attempts attempts, PhotoStore photos) : IQueryHandler<GetAttemptImageQuery, PhotoContent>
{
    public async Task<Result<PhotoContent>> Handle(GetAttemptImageQuery q, CancellationToken ct)
    {
        if (await attempts.MeAsync(ct) is not { } student) return CbtErrors.NotAStudent;
        var attempt = await db.TestAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == q.AttemptId && a.StudentId == student.Id, ct);
        var item = attempt?.Items.FirstOrDefault(i => i.Id == q.ItemId);
        if (item is null) return CbtErrors.AttemptNotFound;
        var path = await db.Questions.AsNoTracking().Where(x => x.Id == item.QuestionId).Select(x => x.ImagePath).FirstOrDefaultAsync(ct);
        return await photos.OpenAsync(path, ct);
    }
}
