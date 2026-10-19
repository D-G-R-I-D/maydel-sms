using Maydel.Domain.Communication;

namespace Maydel.Domain.Tests.Communication;

public sealed class AnnouncementTests
{
    [Theory]
    [InlineData("Inter-House Sports 2026!", "inter-house-sports-2026")]
    [InlineData("  Admission forms   now on sale ", "admission-forms-now-on-sale")]
    [InlineData("!!!", "post")]
    public void Slugs_are_lowercase_words_joined_by_dashes(string title, string slug) => Assert.Equal(slug, Announcement.SlugFor(title));

    [Fact]
    public void Website_features_need_a_public_post_and_sensible_links_and_dates()
    {
        var post = new Announcement { Title = "Forms on sale", Body = "x", Audience = Audience.Parents, IsFeatured = true };
        Assert.Equal("audience", post.Check()?.Field);
        post.Audience = Audience.Everyone;
        post.LinkUrl = "javascript:alert(1)";
        Assert.Equal("linkUrl", post.Check()?.Field);
        post.LinkUrl = "/admissions/apply";
        Assert.Null(post.Check());
        Assert.Equal("Learn more", post.LinkText);
        (post.ShowFrom, post.ShowUntil) = (new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 19));
        Assert.Equal("showUntil", post.Check()?.Field);
    }

    [Fact]
    public void Notices_show_inside_their_window()
    {
        var notice = new Announcement { Title = "t", Body = "b", ShowFrom = new DateOnly(2026, 10, 1), ShowUntil = new DateOnly(2026, 10, 31) };
        Assert.False(notice.IsShowingOn(new DateOnly(2026, 9, 30)));
        Assert.True(notice.IsShowingOn(new DateOnly(2026, 10, 31)));
        Assert.True(new Announcement { Title = "t", Body = "b" }.IsShowingOn(new DateOnly(2030, 1, 1)));
    }
}
