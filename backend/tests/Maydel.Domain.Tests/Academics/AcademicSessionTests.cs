using Maydel.Domain.Academics;

namespace Maydel.Domain.Tests.Academics;

public sealed class AcademicSessionTests
{
    private static readonly DateOnly Sep14 = new(2026, 9, 14);
    private static readonly DateOnly Jul23 = new(2027, 7, 23);

    private static AcademicSession Session() => new() { Name = "2026/2027", StartsOn = Sep14, EndsOn = Jul23 };

    private static Term Term(TermNumber n, DateOnly start, DateOnly end, DateOnly? midStart = null, DateOnly? midEnd = null) =>
        new() { Number = n, StartsOn = start, EndsOn = end, MidTermStartsOn = midStart, MidTermEndsOn = midEnd };

    [Theory]
    [InlineData("2026/2027", null)]
    [InlineData("2026-2027", "name")]
    [InlineData("2026/2028", "name")]
    [InlineData("26/27", "name")]
    public void Session_name_must_be_two_consecutive_years(string name, string? errorField) =>
        Assert.Equal(errorField, AcademicSession.Validate(name, Sep14, Jul23)?.Field);

    [Fact]
    public void Session_must_end_after_it_starts() =>
        Assert.Equal("endsOn", AcademicSession.Validate("2026/2027", Jul23, Sep14)?.Field);

    [Fact]
    public void Adds_terms_in_order_inside_the_session()
    {
        var s = Session();
        Assert.Null(s.AddTerm(Term(TermNumber.First, Sep14, new(2026, 12, 11), new(2026, 10, 26), new(2026, 10, 30))));
        Assert.Null(s.AddTerm(Term(TermNumber.Second, new(2027, 1, 11), new(2027, 3, 26))));
        Assert.Equal(2, s.Terms.Count);
    }

    [Fact]
    public void Rejects_a_second_term_with_the_same_number()
    {
        var s = Session();
        s.AddTerm(Term(TermNumber.First, Sep14, new(2026, 12, 11)));
        Assert.Equal("term.duplicate", s.AddTerm(Term(TermNumber.First, new(2027, 1, 11), new(2027, 3, 26)))?.Code);
    }

    [Fact]
    public void Rejects_terms_outside_the_session_or_ending_before_they_start()
    {
        var s = Session();
        Assert.Equal("term.outside_session", s.AddTerm(Term(TermNumber.First, new(2026, 9, 1), new(2026, 12, 11)))?.Code);
        Assert.Equal("term.dates", s.AddTerm(Term(TermNumber.First, new(2026, 12, 11), Sep14))?.Code);
    }

    [Fact]
    public void Rejects_overlapping_and_out_of_order_terms()
    {
        var s = Session();
        s.AddTerm(Term(TermNumber.Second, new(2027, 1, 11), new(2027, 3, 26)));
        Assert.Equal("term.overlap", s.AddTerm(Term(TermNumber.Third, new(2027, 3, 20), new(2027, 7, 23)))?.Code);
        Assert.Equal("term.order", s.AddTerm(Term(TermNumber.First, new(2027, 4, 19), new(2027, 7, 23)))?.Code);
    }

    [Theory]
    [InlineData(2026, 10, 26, null, null, null, "term.midterm_incomplete")]
    [InlineData(2026, 10, 30, 2026, 10, 26, "term.midterm_dates")]   // ends before it starts
    [InlineData(2026, 12, 20, 2026, 12, 24, "term.midterm_dates")]   // outside the term
    [InlineData(2026, 10, 26, 2026, 10, 30, null)]
    public void Mid_term_break_must_be_complete_and_inside_the_term(
        int sy, int sm, int sd, int? ey, int? em, int? ed, string? code)
    {
        DateOnly? midEnd = ey is null ? null : new DateOnly(ey.Value, em!.Value, ed!.Value);
        var error = Session().AddTerm(Term(TermNumber.First, Sep14, new(2026, 12, 11), new(sy, sm, sd), midEnd));
        Assert.Equal(code, error?.Code);
    }

    [Fact]
    public void Rescheduling_a_term_ignores_itself_when_checking_overlap()
    {
        var s = Session();
        var first = Term(TermNumber.First, Sep14, new(2026, 12, 11));
        s.AddTerm(first);

        Assert.Null(s.RescheduleTerm(first, Sep14, new(2026, 12, 18), null, null));
        Assert.Equal(new DateOnly(2026, 12, 18), first.EndsOn);
    }

    [Fact]
    public void Session_dates_cannot_shrink_past_its_terms()
    {
        var s = Session();
        s.AddTerm(Term(TermNumber.First, Sep14, new(2026, 12, 11)));

        Assert.Equal("startsOn", s.Reschedule("2026/2027", new(2026, 10, 1), Jul23)?.Field);
        Assert.Equal(Sep14, s.StartsOn); // unchanged on failure
    }
}

public sealed class ClassRoomTests
{
    [Theory]
    [InlineData("A", 30, 0, null)]
    [InlineData(" ", 30, 0, "arm")]
    [InlineData("A", 0, 0, "capacity")]
    [InlineData("A", 201, 0, "capacity")]
    [InlineData("A", 20, 25, "capacity")]
    public void Arm_and_capacity_rules(string arm, int capacity, int enrolled, string? field) =>
        Assert.Equal(field, ClassRoom.Validate(arm, capacity, enrolled)?.Field);

    [Fact]
    public void Subject_codes_are_trimmed_and_upper_cased() => Assert.Equal("MTH", Subject.NormalizeCode(" mth "));
}
