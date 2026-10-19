using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Website.GetPost;

public sealed record GetPostQuery(Guid Id) : IQuery<PostDto>;
