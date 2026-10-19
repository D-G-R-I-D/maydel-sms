using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Website.ListPosts;

/// <summary>All posts for staff (drafts included), newest first; filter by text or published state.</summary>
public sealed record ListPostsQuery(string? Search = null, bool? Published = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<PostDto>>;
