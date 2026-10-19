using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.GetPost;

internal sealed class GetPostHandler(IAppDbContext db) : IQueryHandler<GetPostQuery, PostDto>
{
    public async Task<Result<PostDto>> Handle(GetPostQuery q, CancellationToken ct) =>
        await db.Announcements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == q.Id, ct) is { } a ? a.ToDto() : WebsiteErrors.PostNotFound;
}
