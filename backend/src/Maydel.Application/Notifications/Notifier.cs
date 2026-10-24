using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Notifications;

public sealed record NotificationDto(Guid Id, NotificationKind Kind, string Title, string Body, string? LinkUrl, Guid? StudentId, DateTimeOffset CreatedAt, bool IsRead);

public sealed record NotificationInbox(int Unread, IReadOnlyList<NotificationDto> Items);

/// <summary>Adds personal notifications; they're saved with the caller's changes. Anything already sent with the same key is skipped.</summary>
public sealed class Notifier(IAppDbContext db)
{
    /// <summary>Notifies each of the student's guardians who has a portal login.</summary>
    public async Task<int> GuardiansAsync(Guid studentId, NotificationKind kind, string title, string body, string? link, string dedupeKey, CancellationToken ct)
    {
        var users = await db.StudentGuardians.AsNoTracking().Where(g => g.StudentId == studentId && g.Guardian.UserId != null)
            .Select(g => g.Guardian.UserId!.Value).Distinct().ToListAsync(ct);
        if (users.Count == 0) return 0;
        var sent = await db.Notifications.Where(n => users.Contains(n.UserId) && n.DedupeKey == dedupeKey).Select(n => n.UserId).ToListAsync(ct);
        var pending = db.Notifications.Local.Where(n => n.DedupeKey == dedupeKey).Select(n => n.UserId);
        var to = users.Except(sent).Except(pending).ToList();
        foreach (var uid in to)
            db.Notifications.Add(new Notification { UserId = uid, Kind = kind, Title = title, Body = body, LinkUrl = link, StudentId = studentId, DedupeKey = dedupeKey });
        return to.Count;
    }
}

public sealed record ListMyNotificationsQuery(bool UnreadOnly = false) : IQuery<NotificationInbox>;
public sealed record MarkNotificationReadCommand(Guid Id) : ICommand;
public sealed record MarkAllNotificationsReadCommand : ICommand;

internal sealed class ListMyNotificationsHandler(IAppDbContext db, ICurrentUser me) : IQueryHandler<ListMyNotificationsQuery, NotificationInbox>
{
    public async Task<Result<NotificationInbox>> Handle(ListMyNotificationsQuery q, CancellationToken ct)
    {
        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == me.UserId!.Value);
        var unread = await mine.CountAsync(n => n.ReadAt == null, ct);
        if (q.UnreadOnly) mine = mine.Where(n => n.ReadAt == null);
        var items = await mine.OrderByDescending(n => n.CreatedAt).Take(50)
            .Select(n => new NotificationDto(n.Id, n.Kind, n.Title, n.Body, n.LinkUrl, n.StudentId, n.CreatedAt, n.ReadAt != null)).ToListAsync(ct);
        return new NotificationInbox(unread, items);
    }
}

internal sealed class MarkNotificationReadHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : ICommandHandler<MarkNotificationReadCommand>
{
    public async Task<Result> Handle(MarkNotificationReadCommand c, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == c.Id && x.UserId == me.UserId!.Value, ct);
        if (n is null) return Error.NotFound("notification.not_found", "That notification doesn't exist.");
        if (n.ReadAt is null)
        {
            n.ReadAt = clock.Now.ToUniversalTime();
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }
}

internal sealed class MarkAllNotificationsReadHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : ICommandHandler<MarkAllNotificationsReadCommand>
{
    public async Task<Result> Handle(MarkAllNotificationsReadCommand c, CancellationToken ct)
    {
        var now = clock.Now.ToUniversalTime();
        var unread = await db.Notifications.Where(n => n.UserId == me.UserId!.Value && n.ReadAt == null).ToListAsync(ct);
        foreach (var n in unread) n.ReadAt = now;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
