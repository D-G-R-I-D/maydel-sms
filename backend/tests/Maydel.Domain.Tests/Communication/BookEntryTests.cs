using Maydel.Domain.Communication;

namespace Maydel.Domain.Tests.Communication;

public sealed class BookEntryTests
{
    [Fact]
    public void Only_the_author_can_correct_a_note_and_only_for_15_minutes()
    {
        var author = Guid.NewGuid();
        var written = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
        var entry = new BookEntry { AuthorUserId = author, AuthorName = "Mrs. Obi", Body = "Note", CreatedAt = written };

        Assert.True(entry.CanEdit(author, written.AddMinutes(15)));
        Assert.False(entry.CanEdit(author, written.AddMinutes(16)));
        Assert.False(entry.CanEdit(Guid.NewGuid(), written.AddMinutes(1)));
    }

    [Theory]
    [InlineData(" ", false, "book.empty")]
    [InlineData(null, true, null)]
    [InlineData("Hello", false, null)]
    public void A_note_needs_words_or_a_file(string? body, bool hasFile, string? error) =>
        Assert.Equal(error, BookEntry.CheckBody(body, hasFile)?.Code);
}
