using Maydel.Application.Photos;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Persistence;

/// <summary>
/// The website's starting content (news, gallery, testimonials), taken from the school's current site. Runs once in every
/// environment, production included, and records <see cref="Key"/> so that anything an admin edits or deletes in the CMS is never
/// touched or brought back. Turn off with <c>Seed:StarterContent=false</c> (tests do).
/// </summary>
public sealed class StarterContent(AppDbContext db, PhotoStore photos, IOptions<SeedOptions> options, ILogger<StarterContent> logger)
{
    public const string Key = "site-starter-v1";

    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        if (!options.Value.StarterContent || await db.SeedRuns.AnyAsync(r => r.Key == Key, ct)) return false;

        async Task<string?> Picture(string file, string folder)
        {
            await using var stream = typeof(StarterContent).Assembly.GetManifestResourceStream($"Maydel.Seed.Site.{file}");
            if (stream is null) { logger.LogWarning("Starter picture {File} is missing", file); return null; }
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            var saved = await photos.SaveAsync(buffer.ToArray(), folder, transparent: false, 1600, ct);
            return saved.IsSuccess ? saved.Value.Key : null;
        }

        // News (the school's current announcements).
        db.Announcements.AddRange(
            new Announcement
            {
                Title = "New Academic Session Begins", Slug = "new-academic-session-begins", Category = "Announcement", Audience = Audience.Everyone,
                Summary = "The 2026/2027 academic session commences with a special orientation programme for all new and returning students.",
                Body = "The 2026/2027 academic session commences with a special orientation programme for all new and returning students.",
                PublishedAt = new DateTimeOffset(2026, 9, 14, 8, 0, 0, TimeSpan.FromHours(1)).ToUniversalTime(), ImagePath = await Picture("news-new-session.jpg", "news"), InCarousel = true,
            },
            new Announcement
            {
                Title = "WAEC & NECO Approval Confirmed", Slug = "waec-neco-approval-confirmed", Category = "Milestone", Audience = Audience.Everyone, IsPinned = true,
                Summary = "Maydel Spring Schools is officially approved as an examination centre for both WAEC and NECO.",
                Body = "Maydel Spring Schools is officially approved as an examination centre for both WAEC and NECO — a major milestone for our growing school.",
                PublishedAt = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(1)).ToUniversalTime(), ImagePath = await Picture("news-waec-neco.jpg", "news"), InCarousel = true,
            },
            new Announcement
            {
                Title = "Native Festivity Celebration", Slug = "native-festivity-celebration", Category = "Upcoming", Audience = Audience.Everyone, InTicker = true,
                Summary = "Join us for a joyful celebration of our native heritage. Date to be announced.",
                Body = "Join us for a joyful celebration of our native heritage, with traditional music, dance, attire and customs shared by our school community.\n\nDate to be announced.",
                PublishedAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(1)).ToUniversalTime(), ImagePath = await Picture("news-native-festivity.jpg", "news"), InCarousel = true,
            });

        // Gallery albums. Photos with clearly identifiable children were left out (no consent on record); add them through the CMS.
        (string File, string Title, string? Caption, string Album, bool Featured)[] gallery =
        [
            ("campus-entrance.jpg", "Our school entrance", "Early Years, Primary, Junior and Senior High School and After School Care", "Campus", false),
            ("campus-courtyard.jpg", "The courtyard", null, "Campus", false),
            ("campus-classroom-blocks.jpg", "Classroom blocks", null, "Campus", false),
            ("campus-sports-court.jpg", "Sports court", null, "Campus", false),
            ("campus-sports-pitch.jpg", "Sports pitch", null, "Campus", false),
            ("campus-covered-play-area.jpg", "Covered play area", null, "Campus", false),
            ("campus-shaded-lawn.jpg", "Shaded play lawn", null, "Campus", false),
            ("campus-play-lawn.jpg", "Play time on the lawn", null, "Campus", false),
            ("classroom.jpg", "Our classrooms", null, "Classroom", false),
            ("classroom-lesson.jpg", "A lesson in progress", null, "Classroom", false),
            ("classroom-early-years.jpg", "Early years room", "Small hands change the world", "Classroom", false),
            ("sports-aerobics.jpg", "Aerobics", null, "Sports & Movement", true),
            ("culture-heritage-banner.jpg", "Our Cultural Heritage", null, "Culture", false),
        ];
        var order = 0;
        foreach (var (file, title, caption, album, featured) in gallery)
            if (await Picture(file, "gallery") is { } key)
                db.GalleryPhotos.Add(new GalleryPhoto { Title = title, Caption = caption, Category = album, ImagePath = key, Order = order++, IsFeatured = featured });

        // Testimonials from the current site.
        db.Testimonials.AddRange(
            new Testimonial { Order = 1, Attribution = "Parent of a Year 3 student, Primary School",
                Quote = "The teachers at Maydel Spring Schools know my daughter as an individual. She's grown so much in confidence — not just academically, but as a person." },
            new Testimonial { Order = 2, Attribution = "Parent of a Year 8 student, Junior Secondary",
                Quote = "We chose Maydel Spring Schools because it feels like a family. The environment is calm, safe, and truly child-focused. Our son looks forward to school every morning." },
            new Testimonial { Order = 3, Attribution = "Parent of an SSS 3 graduate, Senior Secondary",
                Quote = "The standard of teaching here is exceptional. My daughter passed her WAEC with flying colours — and she actually enjoyed the process." });

        // Established 2022 (from the current site), only if the admin hasn't set it.
        if (await db.SchoolProfiles.OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct) is { FoundedYear: null } profile) profile.FoundedYear = 2022;

        db.SeedRuns.Add(new SeedRun { Key = Key, RanAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded the website starter content ({Key})", Key);
        return true;
    }
}
