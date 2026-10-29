using Maydel.Domain.Academics;

namespace Maydel.Domain.Tests.Academics;

public sealed class PromotionRulesTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid();

    [Fact]
    public void Moves_up_to_the_same_arm_or_the_only_class_otherwise()
    {
        Assert.Equal(B, PromotionRules.DefaultTarget("gold ", [(A, "A"), (B, "Gold")]));
        Assert.Equal(A, PromotionRules.DefaultTarget("Gold", [(A, "A")]));
        Assert.Null(PromotionRules.DefaultTarget("Gold", [(A, "A"), (B, "B")])); // the school chooses
        Assert.Null(PromotionRules.DefaultTarget("A", []));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(39.9, true)]
    [InlineData(40.0, false)]
    public void Flags_a_session_average_below_40_for_review(double? average, bool flagged) =>
        Assert.Equal(flagged, PromotionRules.NeedsReview((decimal?)average));

    [Fact]
    public void Opens_when_the_third_term_starts_or_after_the_session()
    {
        var session = new AcademicSession { Name = "2026/2027", StartsOn = new(2026, 9, 14), EndsOn = new(2027, 7, 23) };
        Assert.Equal("promotion.no_third_term", PromotionRules.CheckOpen(session, new(2027, 5, 1))?.Code);

        session.Terms.Add(new Term { Number = TermNumber.Third, StartsOn = new(2027, 4, 26), EndsOn = new(2027, 7, 23) });
        Assert.Equal("promotion.too_early", PromotionRules.CheckOpen(session, new(2027, 4, 25))?.Code);
        Assert.Null(PromotionRules.CheckOpen(session, new(2027, 4, 26)));
        Assert.Null(PromotionRules.CheckOpen(new AcademicSession { Name = "2025/2026", EndsOn = new(2026, 7, 24) }, new(2026, 9, 1)));
    }
}
