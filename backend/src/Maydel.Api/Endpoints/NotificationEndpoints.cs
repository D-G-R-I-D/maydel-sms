using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Notifications;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Personal notifications for the signed-in user (E12.8): payments, overdue fees; later homework and the communication book.</summary>
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();
        group.MapGet("", List).WithName("ListMyNotifications").ProducesProblems(401);
        group.MapPost("/{id:guid}/read", Read).WithName("MarkNotificationRead").ProducesProblems(401, 404);
        group.MapPost("/read-all", ReadAll).WithName("MarkAllNotificationsRead").ProducesProblems(401);
    }

    private static async Task<Results<Ok<NotificationInbox>, ProblemHttpResult>> List(bool? unreadOnly, IQueryHandler<ListMyNotificationsQuery, NotificationInbox> h, CancellationToken ct) =>
        (await h.Handle(new ListMyNotificationsQuery(unreadOnly ?? false), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> Read(Guid id, ICommandHandler<MarkNotificationReadCommand> h, CancellationToken ct) =>
        (await h.Handle(new MarkNotificationReadCommand(id), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> ReadAll(ICommandHandler<MarkAllNotificationsReadCommand> h, CancellationToken ct) =>
        (await h.Handle(new MarkAllNotificationsReadCommand(), ct)).ToHttpResult();
}
