using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.Notices;

/// <summary>A notice in the portal (E12.1), with whether this user has read it.</summary>
public sealed record PortalNotice(Guid Id, string Title, string? Summary, string Body, string? Category, string? ImageUrl, string? LinkUrl, string? LinkText,
    bool IsPinned, bool IsFeatured, DateTimeOffset PublishedAt, bool IsRead, string? BodyHtml);

public sealed record NoticeInbox(int Unread, IReadOnlyList<PortalNotice> Notices);

/// <summary>Published notices meant for the caller (parents, students or staff by role; applicants see public ones), newest first.</summary>
public sealed record ListMyNoticesQuery(bool UnreadOnly = false) : IQuery<NoticeInbox>;

public sealed record MarkNoticeReadCommand(Guid Id) : ICommand;

public sealed record MarkAllNoticesReadCommand : ICommand;

internal static class NoticeAudience
{
    /// <summary>The audience flags this user belongs to.</summary>
    public static Audience Of(ICurrentUser me)
    {
        var a = Audience.None;
        if (me.IsInRole(Roles.Parent)) a |= Audience.Parents;
        if (me.IsInRole(Roles.Student)) a |= Audience.Students;
        if (me.IsInRole(Roles.Admin) || me.IsInRole(Roles.Teacher) || me.IsInRole(Roles.Bursar)) a |= Audience.Staff;
        if (me.IsInRole(Roles.Applicant)) a |= Audience.Public;
        return a;
    }

    public static IQueryable<Announcement> For(this IAppDbContext db, Audience audience, DateOnly today) =>
        db.Announcements.AsNoTracking().Where(n => n.PublishedAt != null && (n.Audience & audience) != Audience.None
            && (n.ShowFrom == null || n.ShowFrom <= today) && (n.ShowUntil == null || n.ShowUntil >= today));
}

internal sealed class ListMyNoticesHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : IQueryHandler<ListMyNoticesQuery, NoticeInbox>
{
    public async Task<Result<NoticeInbox>> Handle(ListMyNoticesQuery q, CancellationToken ct)
    {
        var uid = me.UserId!.Value;
        var notices = db.For(NoticeAudience.Of(me), clock.Today);
        var read = db.NoticeReads.Where(r => r.UserId == uid).Select(r => r.AnnouncementId);
        var unread = await notices.CountAsync(n => !read.Contains(n.Id), ct);
        if (q.UnreadOnly) notices = notices.Where(n => !read.Contains(n.Id));
        var list = await notices.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.PublishedAt).Take(50)
            .Select(n => new { n, IsRead = read.Contains(n.Id) }).ToListAsync(ct);
        return new NoticeInbox(unread, [.. list.Select(x => new PortalNotice(x.n.Id, x.n.Title, x.n.Summary, x.n.Body, x.n.Category,
            WebsiteUrls.Image("news", x.n.Id, (x.n.Audience & Audience.Public) == Audience.Public ? x.n.ImagePath : null), x.n.LinkUrl, x.n.LinkText,
            x.n.IsPinned, x.n.IsFeatured, x.n.PublishedAt!.Value, x.IsRead, x.n.BodyHtml))]);
    }
}

internal sealed class MarkNoticeReadHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : ICommandHandler<MarkNoticeReadCommand>
{
    public async Task<Result> Handle(MarkNoticeReadCommand c, CancellationToken ct)
    {
        var uid = me.UserId!.Value;
        if (!await db.For(NoticeAudience.Of(me), clock.Today).AnyAsync(n => n.Id == c.Id, ct)) return WebsiteErrors.PostNotFound;
        if (!await db.NoticeReads.AnyAsync(r => r.UserId == uid && r.AnnouncementId == c.Id, ct))
        {
            db.NoticeReads.Add(new NoticeRead { UserId = uid, AnnouncementId = c.Id, ReadAt = clock.Now.ToUniversalTime() });
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }
}

internal sealed class MarkAllNoticesReadHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : ICommandHandler<MarkAllNoticesReadCommand>
{
    public async Task<Result> Handle(MarkAllNoticesReadCommand c, CancellationToken ct)
    {
        var uid = me.UserId!.Value;
        var read = db.NoticeReads.Where(r => r.UserId == uid).Select(r => r.AnnouncementId);
        var ids = await db.For(NoticeAudience.Of(me), clock.Today).Where(n => !read.Contains(n.Id)).Select(n => n.Id).ToListAsync(ct);
        var now = clock.Now.ToUniversalTime();
        db.NoticeReads.AddRange(ids.Select(id => new NoticeRead { UserId = uid, AnnouncementId = id, ReadAt = now }));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
