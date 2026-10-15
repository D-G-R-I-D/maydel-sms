using Maydel.Domain.Finance;

namespace Maydel.Domain.Tests.Finance;

public sealed class FeeRulesTests
{
    [Theory]
    [InlineData("Tuition", 150000, null)]
    [InlineData(" ", 1, "fee.name")]
    [InlineData("Bus", 0, "fee.amount")]
    [InlineData("Bus", 10.005, "fee.amount")]
    public void Fee_items_need_a_name_and_a_positive_amount(string name, decimal amount, string? code) =>
        Assert.Equal(code, FeeRules.CheckFeeItem(name, amount)?.Code);

    [Fact]
    public void Eldest_child_is_first_unknown_birthdays_last()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        var ranks = FeeRules.SiblingRanks([
            (a, new DateOnly(2018, 5, 1), new DateOnly(2024, 9, 1)),
            (b, null, new DateOnly(2020, 9, 1)),
            (c, new DateOnly(2015, 1, 1), new DateOnly(2026, 9, 1)),
        ]);
        Assert.Equal([1, 2, 3], new[] { ranks[c], ranks[a], ranks[b] });
    }

    [Theory]
    [InlineData(SiblingDiscountMode.Percentage, 1, 0)]
    [InlineData(SiblingDiscountMode.Percentage, 2, 15000)]      // 10% of 150,000
    [InlineData(SiblingDiscountMode.Percentage, 3, 30000)]      // 20%
    [InlineData(SiblingDiscountMode.FixedAmount, 2, 10)]
    [InlineData(SiblingDiscountMode.FixedAmount, 4, 20)]
    [InlineData(SiblingDiscountMode.None, 3, 0)]
    public void Sibling_discount_depends_on_the_mode_and_birth_order(SiblingDiscountMode mode, int rank, decimal expected)
    {
        var (second, third) = mode == SiblingDiscountMode.FixedAmount ? (10m, 20m) : (10m, 20m);
        Assert.Equal(expected, FeeRules.SiblingDiscount(mode, second, third, rank, 150000));
    }

    [Fact]
    public void A_fixed_discount_never_exceeds_the_fees() =>
        Assert.Equal(5000, FeeRules.SiblingDiscount(SiblingDiscountMode.FixedAmount, 20000, 20000, 2, 5000));

    [Fact]
    public void Percentages_above_100_are_refused() =>
        Assert.Equal("fee.sibling_percent", FeeRules.CheckSiblingSettings(SiblingDiscountMode.Percentage, 120, 10)?.Code);

    [Theory]
    [InlineData(new[] { 60.0, 40.0 }, new[] { 1, 30 }, null)]
    [InlineData(new[] { 50.0, 30.0, 20.0 }, new[] { 1, 20, 40 }, null)]
    [InlineData(new[] { 100.0 }, new[] { 1 }, "fee.instalment_count")]
    [InlineData(new[] { 25.0, 25.0, 25.0, 25.0 }, new[] { 1, 2, 3, 4 }, "fee.instalment_count")]
    [InlineData(new[] { 60.0, 30.0 }, new[] { 1, 30 }, "fee.instalment_total")]
    [InlineData(new[] { 60.0, 40.0 }, new[] { 30, 1 }, "fee.instalment_dates")]
    public void Instalment_plans_have_2_or_3_parts_adding_to_100_in_date_order(double[] percents, int[] days, string? code)
    {
        var start = new DateOnly(2026, 9, 13);
        var parts = percents.Select((p, i) => ((decimal)p, start.AddDays(days[i]))).ToList();
        Assert.Equal(code, FeeRules.CheckInstalments(parts)?.Code);
    }

    [Fact]
    public void Splitting_rounds_to_kobo_and_adds_up_exactly()
    {
        var amounts = FeeRules.Split(100000.01m, [33.33m, 33.33m, 33.34m]);
        Assert.Equal(100000.01m, amounts.Sum());
        Assert.Equal([33330.00m, 33330.00m, 33340.01m], amounts);
    }

    [Fact]
    public void Invoice_numbers_carry_the_session() => Assert.Equal("INV-2026-2027-0007", FeeRules.InvoiceNumber("2026/2027", 7));
}
