using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Maydel.Api.Common;
using Maydel.Domain.Admissions;
using Maydel.Domain.Communication;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Api.Features;

public sealed record ApplyRequest(
    [property: Required, MaxLength(150)] string ParentName,
    [property: Required, Phone, MaxLength(32)] string Phone,
    [property: EmailAddress, MaxLength(256)] string? Email,
    [property: Required, MaxLength(150)] string ChildName,
    [property: Range(1, 20)] int ChildAge,
    [property: Required, MaxLength(100)] string ClassInterested,
    [property: MaxLength(4000)] string? Message);

public sealed record ContactRequest(
    [property: Required, MaxLength(150)] string Name,
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: MaxLength(100)] string? Subject,
    [property: Required, MaxLength(4000)] string Message);

public sealed record SubscribeRequest([property: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record NewsItem(Guid Id, string Title, string Body, string? Category, string? ImageUrl, DateTimeOffset? PublishedAt);
public sealed record EventItem(Guid Id, string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Location);
public sealed record PublicStaff(Guid Id, string FullName, string? JobTitle, string? Bio, string? PhotoUrl);
public sealed record PublicPhoto(Guid Id, string Title, string? Caption, string Category, string ImageUrl, bool IsFeatured);

/// <summary>Anonymous endpoints that power the marketing website. Forms are rate-limited per IP.</summary>
public static class PublicEndpoints
{
    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/public").WithTags("Public").AllowAnonymous();

        g.MapGet("/news", async (AppDbContext db, int? take, CancellationToken ct) =>
            await db.Announcements.AsNoTracking()
                .Where(a => a.PublishedAt != null && (a.Audience & Audience.Public) != 0)
                .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishedAt)
                .Take(Math.Clamp(take ?? 6, 1, 50))
                .Select(a => new NewsItem(a.Id, a.Title, a.Body, a.Category, Files.Url(a.ImagePath), a.PublishedAt))
                .ToListAsync(ct));

        g.MapGet("/events", async (AppDbContext db, TimeProvider clock, CancellationToken ct) =>
            await db.Events.AsNoTracking()
                .Where(e => (e.Audience & Audience.Public) != 0 && (e.EndsAt ?? e.StartsAt) >= clock.GetUtcNow())
                .OrderBy(e => e.StartsAt).Take(20)
                .Select(e => new EventItem(e.Id, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Location))
                .ToListAsync(ct));

        g.MapGet("/staff", async (AppDbContext db, CancellationToken ct) =>
            await db.Staff.AsNoTracking().Where(s => s.IsActive && s.ShowOnWebsite)
                .OrderBy(s => s.WebsiteOrder).ThenBy(s => s.FullName)
                .Select(s => new PublicStaff(s.Id, s.FullName, s.JobTitle, s.Bio, Files.Url(s.PhotoPath)))
                .ToListAsync(ct));

        g.MapGet("/gallery", async (AppDbContext db, CancellationToken ct) =>
            await db.GalleryPhotos.AsNoTracking().OrderBy(p => p.Order)
                .Select(p => new PublicPhoto(p.Id, p.Title, p.Caption, p.Category, Files.Url(p.ImagePath)!, p.IsFeatured))
                .ToListAsync(ct));

        g.MapGet("/calendar", async (AppDbContext db, CancellationToken ct) =>
            await db.Terms.AsNoTracking().Where(t => t.Session.IsCurrent).OrderBy(t => t.Number)
                .Select(t => new { t.Number, Session = t.Session.Name, t.StartsOn, t.EndsOn, t.MidTermStartsOn, t.MidTermEndsOn })
                .ToListAsync(ct));

        g.MapPost("/applications", async (ApplyRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var application = new AdmissionApplication
            {
                ReferenceNumber = $"APP-{DateTime.UtcNow:yyMM}-{RandomNumberGenerator.GetHexString(6)}",
                ParentName = req.ParentName.Trim(),
                Phone = req.Phone.Trim(),
                Email = req.Email?.Trim(),
                ChildName = req.ChildName.Trim(),
                ChildAge = req.ChildAge,
                ClassInterested = req.ClassInterested.Trim(),
                Message = req.Message?.Trim(),
            };
            db.Applications.Add(application);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admissions/{application.Id}", new { application.ReferenceNumber });
        }).RequireRateLimiting(RateLimits.PublicForms);

        g.MapPost("/contact", async (ContactRequest req, AppDbContext db, CancellationToken ct) =>
        {
            db.ContactMessages.Add(new ContactMessage { Name = req.Name.Trim(), Email = req.Email.Trim(), Subject = req.Subject, Message = req.Message.Trim() });
            await db.SaveChangesAsync(ct);
            return Results.Accepted();
        }).RequireRateLimiting(RateLimits.PublicForms);

        g.MapPost("/newsletter", async (SubscribeRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            // Idempotent, and never reveals whether an address was already subscribed.
            if (!await db.NewsletterSubscribers.AnyAsync(s => s.Email == email, ct))
            {
                db.NewsletterSubscribers.Add(new NewsletterSubscriber { Email = email });
                await db.SaveChangesAsync(ct);
            }
            return Results.Accepted();
        }).RequireRateLimiting(RateLimits.PublicForms);
    }
}
