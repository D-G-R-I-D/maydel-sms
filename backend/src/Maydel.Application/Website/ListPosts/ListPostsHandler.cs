using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.ListPosts;

internal sealed class ListPostsHandler(IAppDbContext db) : IQueryHandler<ListPostsQuery, PagedResult<PostDto>>
{
    public async Task<Result<PagedResult<PostDto>>> Handle(ListPostsQuery q, CancellationToken ct)
    {
        var posts = db.Announcements.AsNoTracking();
        if (q.Published is { } published) posts = posts.Where(a => (a.PublishedAt != null) == published);
        foreach (var word in (q.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
        {
            var w = word.ToLower();
            posts = posts.Where(a => a.Title.ToLower().Contains(w) || (a.Category != null && a.Category.ToLower().Contains(w)));
        }
        var page = await posts.OrderByDescending(a => a.CreatedAt).ToPagedAsync(q.Page, q.PageSize, ct);
        return new PagedResult<PostDto>([.. page.Items.Select(a => a.ToDto())], page.Page, page.PageSize, page.TotalCount);
    }
}
