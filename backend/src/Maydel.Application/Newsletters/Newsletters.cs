using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Website;
using Maydel.Domain.Academics;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Newsletters;

public sealed record NewsletterInput(string Title, string? Preheader, string BodyHtml, NewsletterAudience Audience, SchoolSection? Section, Guid? ClassRoomId,
    bool PublishOnWebsite);

/// <summary>Delivery so far: <c>Pending</c> includes copies waiting for tomorrow's allowance.</summary>
public sealed record NewsletterDelivery(int Pending, int Sent, int Failed);

public sealed record NewsletterSummary(Guid Id, string Title, NewsletterAudience Audience, NewsletterStatus Status, DateTimeOffset? ScheduledFor, DateTimeOffset? SentAt,
    int Recipients, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record NewsletterDto(Guid Id, string Title, string? Preheader, string BodyHtml, NewsletterAudience Audience, SchoolSection? Section, Guid? ClassRoomId,
    bool PublishOnWebsite, NewsletterStatus Status, DateTimeOffset? ScheduledFor, DateTimeOffset? SentAt, int Recipients, NewsletterDelivery Delivery, string? WebUrl,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record RecipientCount(int Parents, int Staff, int Subscribers, int Total);

public sealed record NewsletterSendInput(DateTimeOffset? At);

public sealed record NewsletterTestInput(string? Email);

public sealed record PublicNewsletterSummary(string Slug, string Title, string? Preheader, DateTimeOffset SentAt);

public sealed record PublicNewsletter(string Slug, string Title, string? Preheader, string BodyHtml, DateTimeOffset SentAt);

public static class NewsletterErrors
{
    public static readonly Error NotFound = Error.NotFound("newsletter.not_found", "That newsletter doesn't exist.");
    public static readonly Error NotEditable = Error.Rule("newsletter.not_editable", "Only drafts can be changed. Cancel the schedule first, or copy a sent newsletter into a new one.");
    public static readonly Error NoRecipients = Error.Rule("newsletter.no_recipients", "Nobody would receive this newsletter. Check the audience.");
    public static readonly Error BadToken = Error.NotFound("newsletter.link", "This link isn't valid any more.");
}

/// <summary>Who receives a newsletter: deduplicated by address, unsubscribed addresses removed.</summary>
public sealed class NewsletterAudiences(IAppDbContext db)
{
    public sealed record Recipient(string Email, string? Name, Guid? GuardianId, NewsletterAudience Group);

    public async Task<IReadOnlyList<Recipient>> ResolveAsync(Newsletter n, CancellationToken ct)
    {
        var all = new List<Recipient>();
        if (n.Audience.HasFlag(NewsletterAudience.Parents))
        {
            var students = db.Students.AsNoTracking().Where(s => s.Status == Domain.People.StudentStatus.Active);
            if (n.ClassRoomId is { } room) students = students.Where(s => s.ClassRoomId == room);
            if (n.Section is { } section)
            {
                var rooms = db.ClassRooms.Where(r => r.ClassLevel.Section == section).Select(r => r.Id);
                students = students.Where(s => s.ClassRoomId != null && rooms.Contains(s.ClassRoomId.Value));
            }
            var ids = students.Select(s => s.Id);
            all.AddRange(await db.StudentGuardians.AsNoTracking()
                .Where(g => ids.Contains(g.StudentId) && g.Guardian.Email != null && g.Guardian.Email != "")
                .Select(g => new Recipient(g.Guardian.Email!, g.Guardian.FullName, g.GuardianId, NewsletterAudience.Parents)).ToListAsync(ct));
        }
        if (n.Audience.HasFlag(NewsletterAudience.Staff))
            all.AddRange(await db.Staff.AsNoTracking().Where(s => s.IsActive && s.Email != null && s.Email != "")
                .Select(s => new Recipient(s.Email!, s.FullName, null, NewsletterAudience.Staff)).ToListAsync(ct));
        if (n.Audience.HasFlag(NewsletterAudience.Subscribers))
            all.AddRange(await db.NewsletterSubscribers.AsNoTracking().Where(s => s.IsActive && s.ConfirmedAt != null)
                .Select(s => new Recipient(s.Email, null, null, NewsletterAudience.Subscribers)).ToListAsync(ct));

        var optedOut = (await db.NewsletterOptOuts.AsNoTracking().Select(o => o.Email).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. all.Select(r => r with { Email = r.Email.Trim() })
            .Where(r => MailAddress.TryCreate(r.Email, out _) && !optedOut.Contains(r.Email))
            .GroupBy(r => r.Email.ToLowerInvariant()).Select(g => g.First())];
    }

    /// <summary>Queues one copy per recipient with its own unsubscribe token; marks the newsletter sent.</summary>
    public async Task<int> EnqueueAsync(Newsletter n, DateTimeOffset now, CancellationToken ct)
    {
        var recipients = await ResolveAsync(n, ct);
        foreach (var r in recipients)
            db.OutboxEmails.Add(new OutboxEmail
            {
                Kind = EmailKind.Newsletter, ToAddress = r.Email, ToName = r.Name, Subject = n.Title, Body = n.BodyText,
                NewsletterId = n.Id, GuardianId = r.GuardianId, UnsubscribeToken = Tokens.New(), NextAttemptAt = now,
            });
        (n.Status, n.SentAt, n.ScheduledFor, n.Recipients) = (NewsletterStatus.Sent, now, null, recipients.Count);
        if (n.PublishOnWebsite && n.Slug.Length == 0) n.Slug = await UniqueSlugAsync(n, ct);
        return recipients.Count;
    }

    private async Task<string> UniqueSlugAsync(Newsletter n, CancellationToken ct)
    {
        var root = Announcement.SlugFor(n.Title);
        var taken = await db.Newsletters.Where(x => x.Id != n.Id && (x.Slug == root || x.Slug.StartsWith(root + "-"))).Select(x => x.Slug).ToListAsync(ct);
        if (!taken.Contains(root)) return root;
        for (var i = 2; ; i++)
            if (!taken.Contains($"{root}-{i}")) return $"{root}-{i}";
    }
}

internal static class Tokens
{
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal static class NewsletterMapping
{
    public static async Task<NewsletterDto> DtoAsync(this IAppDbContext db, Newsletter n, CancellationToken ct)
    {
        var copies = await db.OutboxEmails.AsNoTracking().Where(e => e.NewsletterId == n.Id && e.UnsubscribeToken != null)
            .GroupBy(e => e.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int C(params EmailStatus[] s) => copies.Where(x => s.Contains(x.Key)).Sum(x => x.Count);
        return new NewsletterDto(n.Id, n.Title, n.Preheader, n.BodyHtml, n.Audience, n.Section, n.ClassRoomId, n.PublishOnWebsite, n.Status, n.ScheduledFor,
            n.SentAt, n.Recipients, new NewsletterDelivery(C(EmailStatus.Pending), C(EmailStatus.Sent), C(EmailStatus.Failed, EmailStatus.Skipped)),
            n.PublishOnWebsite && n.Slug.Length > 0 ? $"/newsletters/{n.Slug}" : null, n.CreatedAt, n.UpdatedAt);
    }
}

// ---------- Admin ----------

public sealed record ListNewslettersQuery(int? Page = null, int? PageSize = null) : IQuery<PagedResult<NewsletterSummary>>;
public sealed record GetNewsletterQuery(Guid Id) : IQuery<NewsletterDto>;
public sealed record SaveNewsletterCommand(Guid? Id, NewsletterInput Input) : ICommand<NewsletterDto>;
public sealed record DeleteNewsletterCommand(Guid Id) : ICommand;
public sealed record CountRecipientsQuery(Guid Id) : IQuery<RecipientCount>;

/// <summary>Sends one copy now to <c>Email</c> (default: your own address), marked as a test.</summary>
public sealed record TestNewsletterCommand(Guid Id, string? Email) : ICommand;

/// <summary>Sends now, or schedules for <c>At</c> (a future time).</summary>
public sealed record SendNewsletterCommand(Guid Id, DateTimeOffset? At) : ICommand<NewsletterDto>;
public sealed record CancelScheduleCommand(Guid Id) : ICommand<NewsletterDto>;

/// <summary>Sends scheduled newsletters whose time has come (background worker).</summary>
public sealed record SendDueNewslettersCommand(DateTimeOffset? Now = null) : ICommand<int>;

internal sealed class ListNewslettersHandler(IAppDbContext db) : IQueryHandler<ListNewslettersQuery, PagedResult<NewsletterSummary>>
{
    public async Task<Result<PagedResult<NewsletterSummary>>> Handle(ListNewslettersQuery q, CancellationToken ct) =>
        await db.Newsletters.AsNoTracking().OrderByDescending(n => n.CreatedAt)
            .Select(n => new NewsletterSummary(n.Id, n.Title, n.Audience, n.Status, n.ScheduledFor, n.SentAt, n.Recipients, n.CreatedAt, n.UpdatedAt))
            .ToPagedAsync(q.Page, q.PageSize, ct);
}

internal sealed class GetNewsletterHandler(IAppDbContext db) : IQueryHandler<GetNewsletterQuery, NewsletterDto>
{
    public async Task<Result<NewsletterDto>> Handle(GetNewsletterQuery q, CancellationToken ct) =>
        await db.Newsletters.AsNoTracking().FirstOrDefaultAsync(n => n.Id == q.Id, ct) is { } n ? await db.DtoAsync(n, ct) : NewsletterErrors.NotFound;
}

internal sealed class SaveNewsletterHandler(IAppDbContext db) : ICommandHandler<SaveNewsletterCommand, NewsletterDto>
{
    public async Task<Result<NewsletterDto>> Handle(SaveNewsletterCommand c, CancellationToken ct)
    {
        Newsletter n;
        if (c.Id is { } id)
        {
            if (await db.Newsletters.FirstOrDefaultAsync(x => x.Id == id, ct) is not { } found) return NewsletterErrors.NotFound;
            if (!found.IsEditable) return NewsletterErrors.NotEditable;
            n = found;
        }
        else db.Newsletters.Add(n = new Newsletter { Title = "", BodyHtml = "", BodyText = "" });

        var i = c.Input;
        if (i.BodyHtml is { Length: > RichText.MaxLength }) return Error.Validation("bodyHtml", "The newsletter is too long.");
        var rich = RichText.Clean(i.BodyHtml);
        (n.Title, n.Preheader, n.BodyHtml, n.BodyText, n.Audience, n.Section, n.ClassRoomId, n.PublishOnWebsite) =
            (i.Title ?? "", string.IsNullOrWhiteSpace(i.Preheader) ? null : i.Preheader.Trim(), rich?.Html ?? "", rich?.Text ?? "", i.Audience, i.Section,
                i.ClassRoomId, i.PublishOnWebsite);
        if (n.Check() is { } invalid) return invalid.ToError();
        if (n.ClassRoomId is { } room && !await db.ClassRooms.AnyAsync(r => r.Id == room, ct)) return Error.Validation("classRoomId", "That class doesn't exist.");
        await db.SaveChangesAsync(ct);
        return await db.DtoAsync(n, ct);
    }
}

internal sealed class DeleteNewsletterHandler(IAppDbContext db) : ICommandHandler<DeleteNewsletterCommand>
{
    public async Task<Result> Handle(DeleteNewsletterCommand c, CancellationToken ct)
    {
        if (await db.Newsletters.FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } n) return NewsletterErrors.NotFound;
        if (!n.IsEditable) return NewsletterErrors.NotEditable;
        db.Newsletters.Remove(n);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class CountRecipientsHandler(IAppDbContext db, NewsletterAudiences audiences) : IQueryHandler<CountRecipientsQuery, RecipientCount>
{
    public async Task<Result<RecipientCount>> Handle(CountRecipientsQuery q, CancellationToken ct)
    {
        if (await db.Newsletters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct) is not { } n) return NewsletterErrors.NotFound;
        var r = await audiences.ResolveAsync(n, ct);
        return new RecipientCount(r.Count(x => x.Group == NewsletterAudience.Parents), r.Count(x => x.Group == NewsletterAudience.Staff),
            r.Count(x => x.Group == NewsletterAudience.Subscribers), r.Count);
    }
}

internal sealed class TestNewsletterHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : ICommandHandler<TestNewsletterCommand>
{
    public async Task<Result> Handle(TestNewsletterCommand c, CancellationToken ct)
    {
        if (await db.Newsletters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } n) return NewsletterErrors.NotFound;
        var to = string.IsNullOrWhiteSpace(c.Email)
            ? await db.Users.AsNoTracking().Where(u => u.Id == me.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct)
            : c.Email.Trim();
        if (to is null || !MailAddress.TryCreate(to, out _)) return Error.Validation("email", "Enter the address to send the test to.");
        // A test copy has no unsubscribe token, so it never counts as delivery.
        db.OutboxEmails.Add(new OutboxEmail
        {
            Kind = EmailKind.Newsletter, ToAddress = to, Subject = $"[Test] {n.Title}", Body = n.BodyText, NewsletterId = n.Id, NextAttemptAt = clock.Now.ToUniversalTime(),
        });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class SendNewsletterHandler(IAppDbContext db, NewsletterAudiences audiences, ISchoolClock clock) : ICommandHandler<SendNewsletterCommand, NewsletterDto>
{
    public async Task<Result<NewsletterDto>> Handle(SendNewsletterCommand c, CancellationToken ct)
    {
        if (await db.Newsletters.FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } n) return NewsletterErrors.NotFound;
        if (!n.IsEditable) return NewsletterErrors.NotEditable;
        var now = clock.Now.ToUniversalTime();
        if (c.At is { } at && at.ToUniversalTime() > now.AddMinutes(1))
        {
            if (at > now.AddDays(90)) return Error.Validation("at", "Schedule it within the next 90 days.");
            if ((await audiences.ResolveAsync(n, ct)).Count == 0) return NewsletterErrors.NoRecipients;
            (n.Status, n.ScheduledFor) = (NewsletterStatus.Scheduled, at.ToUniversalTime());
        }
        else if (await audiences.EnqueueAsync(n, now, ct) == 0) return NewsletterErrors.NoRecipients;
        await db.SaveChangesAsync(ct);
        return await db.DtoAsync(n, ct);
    }
}

internal sealed class CancelScheduleHandler(IAppDbContext db) : ICommandHandler<CancelScheduleCommand, NewsletterDto>
{
    public async Task<Result<NewsletterDto>> Handle(CancelScheduleCommand c, CancellationToken ct)
    {
        if (await db.Newsletters.FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } n) return NewsletterErrors.NotFound;
        if (n.Status != NewsletterStatus.Scheduled) return Error.Rule("newsletter.not_scheduled", "This newsletter isn't scheduled.");
        (n.Status, n.ScheduledFor) = (NewsletterStatus.Draft, null);
        await db.SaveChangesAsync(ct);
        return await db.DtoAsync(n, ct);
    }
}

internal sealed class SendDueNewslettersHandler(IAppDbContext db, NewsletterAudiences audiences, ISchoolClock clock) : ICommandHandler<SendDueNewslettersCommand, int>
{
    public async Task<Result<int>> Handle(SendDueNewslettersCommand c, CancellationToken ct)
    {
        var now = (c.Now ?? clock.Now).ToUniversalTime();
        var due = await db.Newsletters.Where(n => n.Status == NewsletterStatus.Scheduled && n.ScheduledFor <= now).ToListAsync(ct);
        foreach (var n in due) await audiences.EnqueueAsync(n, clock.Now.ToUniversalTime(), ct);
        await db.SaveChangesAsync(ct);
        return due.Count;
    }
}

// ---------- Public ----------

public sealed record ListPublicNewslettersQuery : IQuery<IReadOnlyList<PublicNewsletterSummary>>;
public sealed record GetPublicNewsletterQuery(string Slug) : IQuery<PublicNewsletter>;

/// <summary>From the link in a newsletter: stops newsletters to that address (school notices still arrive).</summary>
public sealed record UnsubscribeNewsletterCommand(string Token) : ICommand;

/// <summary>From the link in the sign-up confirmation email (double opt-in).</summary>
public sealed record ConfirmSubscriptionCommand(string Token) : ICommand;

internal sealed class ListPublicNewslettersHandler(IAppDbContext db) : IQueryHandler<ListPublicNewslettersQuery, IReadOnlyList<PublicNewsletterSummary>>
{
    public async Task<Result<IReadOnlyList<PublicNewsletterSummary>>> Handle(ListPublicNewslettersQuery q, CancellationToken ct) =>
        await db.Newsletters.AsNoTracking().Where(n => n.Status == NewsletterStatus.Sent && n.PublishOnWebsite && n.Slug != "")
            .OrderByDescending(n => n.SentAt).Take(50)
            .Select(n => new PublicNewsletterSummary(n.Slug, n.Title, n.Preheader, n.SentAt!.Value)).ToListAsync(ct);
}

internal sealed class GetPublicNewsletterHandler(IAppDbContext db) : IQueryHandler<GetPublicNewsletterQuery, PublicNewsletter>
{
    public async Task<Result<PublicNewsletter>> Handle(GetPublicNewsletterQuery q, CancellationToken ct) =>
        await db.Newsletters.AsNoTracking().Where(n => n.Status == NewsletterStatus.Sent && n.PublishOnWebsite && n.Slug == q.Slug)
            .Select(n => new PublicNewsletter(n.Slug, n.Title, n.Preheader, n.BodyHtml, n.SentAt!.Value)).FirstOrDefaultAsync(ct) is { } found
            ? found : NewsletterErrors.NotFound;
}

internal sealed class UnsubscribeNewsletterHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<UnsubscribeNewsletterCommand>
{
    public async Task<Result> Handle(UnsubscribeNewsletterCommand c, CancellationToken ct)
    {
        var token = (c.Token ?? "").Trim();
        if (token.Length is < 16 or > 64) return NewsletterErrors.BadToken;
        var email = await db.OutboxEmails.AsNoTracking().Where(e => e.UnsubscribeToken == token).Select(e => e.ToAddress).FirstOrDefaultAsync(ct);
        if (email is null) return NewsletterErrors.BadToken;
        var key = email.Trim().ToLowerInvariant();
        if (!await db.NewsletterOptOuts.AnyAsync(o => o.Email == key, ct))
            db.NewsletterOptOuts.Add(new NewsletterOptOut { Email = key, CreatedAt = clock.Now.ToUniversalTime() });
        if (await db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Email == key, ct) is { } subscriber) subscriber.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class ConfirmSubscriptionHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<ConfirmSubscriptionCommand>
{
    public async Task<Result> Handle(ConfirmSubscriptionCommand c, CancellationToken ct)
    {
        var token = (c.Token ?? "").Trim();
        if (token.Length is < 16 or > 64) return NewsletterErrors.BadToken;
        var hash = Tokens.Hash(token);
        if (await db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.ConfirmTokenHash == hash, ct) is not { } s) return NewsletterErrors.BadToken;
        (s.ConfirmedAt, s.ConfirmTokenHash, s.IsActive) = (clock.Now.ToUniversalTime(), null, true);
        // Signing up again undoes an earlier unsubscribe.
        if (await db.NewsletterOptOuts.FirstOrDefaultAsync(o => o.Email == s.Email, ct) is { } optOut) db.NewsletterOptOuts.Remove(optOut);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Queues the "confirm your subscription" email for a website sign-up.</summary>
public sealed class SubscriptionEmails(IAppDbContext db, IOptions<EmailOptions> options)
{
    public async Task QueueAsync(Domain.Admissions.NewsletterSubscriber s, DateTimeOffset now, CancellationToken ct)
    {
        var token = Tokens.New();
        s.ConfirmTokenHash = Tokens.Hash(token);
        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? options.Value.FromName;
        var link = $"{(options.Value.PortalUrl ?? "").TrimEnd('/')}/newsletter/confirm?t={token}";
        db.OutboxEmails.Add(new OutboxEmail
        {
            Kind = EmailKind.NewsletterConfirm, ToAddress = s.Email, Subject = $"Please confirm your subscription – {school}",
            Body = $"Hello,\n\nThank you for signing up for {school}'s newsletter. Please confirm that this is your address: {link}\n\nIf you didn't sign up, ignore this email and you won't hear from us.\n\n{school}",
            NextAttemptAt = now,
        });
    }
}
