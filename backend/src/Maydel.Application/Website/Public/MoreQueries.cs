using System.Net.Mail;
using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.Public;

public sealed record CalendarTerm(string Term, DateOnly StartsOn, DateOnly EndsOn, DateOnly? MidTermStartsOn, DateOnly? MidTermEndsOn);

public sealed record CalendarSession(Guid Id, string Session, bool IsCurrent, IReadOnlyList<CalendarTerm> Terms);

/// <summary>The current session and the next one (when set up), with term and mid-term dates (E7.1).</summary>
public sealed record GetPublicCalendarQuery : IQuery<IReadOnlyList<CalendarSession>>;

/// <summary>
/// Live numbers for the website (E7.1). Counts are rounded down to a tidy figure (e.g. 523 → 500) and shown as "500+";
/// <c>Shown</c> is false when the admin turns them off (then show nothing).
/// </summary>
public sealed record PublicStats(bool Shown, int Students, string StudentsLabel, int Teachers, string TeachersLabel, int Levels, int? FoundedYear, int? YearsOfService);

public sealed record GetPublicStatsQuery : IQuery<PublicStats>;

public sealed record SubscribeInput(string Email, string? Website);

/// <summary>"Stay in the loop" (E7.1): adds the email to the newsletter list (re-subscribing is fine; nothing is revealed).</summary>
public sealed record SubscribeCommand(SubscribeInput Input) : ICommand;

public sealed record PublicTestimonial(Guid Id, string Quote, string Attribution);

public sealed record ListPublicTestimonialsQuery : IQuery<IReadOnlyList<PublicTestimonial>>;

internal sealed class GetPublicCalendarHandler(IAppDbContext db) : IQueryHandler<GetPublicCalendarQuery, IReadOnlyList<CalendarSession>>
{
    public async Task<Result<IReadOnlyList<CalendarSession>>> Handle(GetPublicCalendarQuery q, CancellationToken ct)
    {
        var sessions = await db.Sessions.AsNoTracking().Include(s => s.Terms).OrderBy(s => s.StartsOn).ToListAsync(ct);
        var current = sessions.FirstOrDefault(s => s.IsCurrent);
        if (current is null) return Array.Empty<CalendarSession>();
        var next = sessions.FirstOrDefault(s => s.StartsOn > current.StartsOn);
        return new[] { current, next }.OfType<Domain.Academics.AcademicSession>()
            .Select(s => new CalendarSession(s.Id, s.Name, s.IsCurrent,
                [.. s.Terms.OrderBy(t => t.StartsOn).Select(t => new CalendarTerm($"{t.Number} Term", t.StartsOn, t.EndsOn, t.MidTermStartsOn, t.MidTermEndsOn))]))
            .ToList();
    }
}

internal sealed class GetPublicStatsHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<GetPublicStatsQuery, PublicStats>
{
    public async Task<Result<PublicStats>> Handle(GetPublicStatsQuery q, CancellationToken ct)
    {
        var profile = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        if (profile is { ShowStatsOnWebsite: false }) return new PublicStats(false, 0, "", 0, "", 0, null, null);
        var students = await db.Students.CountAsync(s => s.Status == StudentStatus.Active, ct);
        var teacherRole = await db.Roles.Where(r => r.Name == Roles.Teacher).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var teacherUsers = db.UserRoles.Where(ur => ur.RoleId == teacherRole).Select(ur => ur.UserId);
        var teachers = await db.Staff.CountAsync(s => s.IsActive && s.UserId != null && teacherUsers.Contains(s.UserId.Value), ct);
        var levels = await db.ClassLevels.CountAsync(ct);
        var (st, sl) = Tidy(students);
        var (te, tl) = Tidy(teachers);
        int? years = profile?.FoundedYear is { } founded ? Math.Max(0, clock.Today.Year - founded) : null;
        return new PublicStats(true, st, sl, te, tl, levels, profile?.FoundedYear, years);
    }

    /// <summary>523 → (500, "500+"); 47 → (40, "40+"); 7 → (7, "7").</summary>
    public static (int Value, string Label) Tidy(int n)
    {
        if (n < 10) return (n, n.ToString());
        var step = n < 100 ? 10 : n < 1000 ? 50 : 100;
        var tidy = n / step * step;
        return (tidy, tidy == n ? n.ToString() : $"{tidy}+");
    }
}

internal sealed class SubscribeHandler(IAppDbContext db, Newsletters.SubscriptionEmails confirmations, ISchoolClock clock) : ICommandHandler<SubscribeCommand>
{
    public async Task<Result> Handle(SubscribeCommand c, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(c.Input.Website)) return Result.Success(); // honeypot
        var email = (c.Input.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length > 254 || !MailAddress.TryCreate(email, out _)) return Error.Validation("email", "Enter a valid email address.");
        // Double opt-in: nothing is sent until the address is confirmed; a confirmed, active subscriber needs nothing more.
        var existing = await db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Email == email, ct);
        if (existing is { IsActive: true, ConfirmedAt: not null }) return Result.Success();
        if (existing is null) db.NewsletterSubscribers.Add(existing = new NewsletterSubscriber { Email = email, IsActive = false });
        await confirmations.QueueAsync(existing, clock.Now.ToUniversalTime(), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class ListPublicTestimonialsHandler(IAppDbContext db) : IQueryHandler<ListPublicTestimonialsQuery, IReadOnlyList<PublicTestimonial>>
{
    public async Task<Result<IReadOnlyList<PublicTestimonial>>> Handle(ListPublicTestimonialsQuery q, CancellationToken ct) =>
        await db.Testimonials.AsNoTracking().Where(t => t.IsPublished).OrderBy(t => t.Order).ThenBy(t => t.CreatedAt)
            .Select(t => new PublicTestimonial(t.Id, t.Quote, t.Attribution)).ToListAsync(ct);
}
