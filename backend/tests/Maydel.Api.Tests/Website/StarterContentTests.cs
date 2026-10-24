using Maydel.Api.Tests.Infrastructure;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Maydel.Api.Tests.Website;

/// <summary>The website starter content runs once; deleted items never come back.</summary>
[Collection(nameof(ApiCollection))]
public sealed class StarterContentTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Starter_content_seeds_once_and_respects_deletions()
    {
        await fixture.Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var seeder = new StarterContent(db, sp.GetRequiredService<Maydel.Application.Photos.PhotoStore>(),
                Options.Create(new SeedOptions { StarterContent = true }), NullLogger<StarterContent>.Instance);

            Assert.True(await seeder.RunAsync());
            var waec = await db.Announcements.SingleAsync(a => a.Slug == "waec-neco-approval-confirmed");
            Assert.True(waec.IsPinned && waec.PublishedAt is not null && waec.ImagePath is not null);
            Assert.True(await db.GalleryPhotos.CountAsync(p => p.Category == "Campus") >= 6);
            Assert.Equal(3, await db.Testimonials.CountAsync(t => t.Quote.Contains("Maydel") || t.Quote.Contains("WAEC")));

            // An admin deletes a post: running again changes nothing.
            db.Announcements.Remove(waec);
            await db.SaveChangesAsync();
            Assert.False(await seeder.RunAsync());
            Assert.False(await db.Announcements.AnyAsync(a => a.Slug == "waec-neco-approval-confirmed"));
        });
    }
}
