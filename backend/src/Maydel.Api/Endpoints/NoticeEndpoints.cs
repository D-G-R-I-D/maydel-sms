using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Website.Notices;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Notices for signed-in users (E12.1): parents, students, staff and applicants each see theirs, with an unread count.</summary>
public static class NoticeEndpoints
{
    public static void MapNoticeEndpoints(this IEndpointRouteBuilder app)
    {
        // Any signed-in user (the fallback policy); what they see depends on their roles.
        var notices = app.MapGroup("/api/notices").WithTags("Notices").RequireAuthorization();
        notices.MapGet("", List).WithName("ListMyNotices").ProducesProblems(401);
        notices.MapPost("/{id:guid}/read", Read).WithName("MarkNoticeRead").ProducesProblems(401, 404);
        notices.MapPost("/read-all", ReadAll).WithName("MarkAllNoticesRead").ProducesProblems(401);
    }

    private static async Task<Results<Ok<NoticeInbox>, ProblemHttpResult>> List(bool? unreadOnly, IQueryHandler<ListMyNoticesQuery, NoticeInbox> h, CancellationToken ct) =>
        (await h.Handle(new ListMyNoticesQuery(unreadOnly ?? false), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> Read(Guid id, ICommandHandler<MarkNoticeReadCommand> h, CancellationToken ct) =>
        (await h.Handle(new MarkNoticeReadCommand(id), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> ReadAll(ICommandHandler<MarkAllNoticesReadCommand> h, CancellationToken ct) =>
        (await h.Handle(new MarkAllNoticesReadCommand(), ct)).ToHttpResult();
}
