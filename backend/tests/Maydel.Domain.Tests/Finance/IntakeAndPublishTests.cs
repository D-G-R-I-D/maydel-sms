using Maydel.Domain.Finance;

namespace Maydel.Domain.Tests.Finance;

public sealed class IntakeAndPublishTests
{
    private static readonly DateOnly Starts = new(2026, 9, 14), Ends = new(2026, 12, 11);

    [Theory]
    [InlineData("2026-07-01", true)]    // summer admission before September
    [InlineData("2026-10-20", true)]    // joined mid-term
    [InlineData("2026-05-01", false)]   // more than 120 days before: last session's intake
    [InlineData("2027-01-11", false)]   // admitted for next term
    public void New_intake_means_admitted_for_this_term(string admitted, bool expected) =>
        Assert.Equal(expected, FeeRules.IsNewIntake(DateOnly.Parse(admitted), Starts, Ends));

    [Theory]
    [InlineData(FeeAudience.All, true, true)]
    [InlineData(FeeAudience.All, false, true)]
    [InlineData(FeeAudience.NewIntake, true, true)]
    [InlineData(FeeAudience.NewIntake, false, false)]
    [InlineData(FeeAudience.Returning, true, false)]
    [InlineData(FeeAudience.Returning, false, true)]
    public void Fee_items_apply_to_their_audience(FeeAudience audience, bool newIntake, bool applies) =>
        Assert.Equal(applies, FeeRules.Applies(audience, newIntake));

    [Fact]
    public void Publishing_happens_once_and_never_for_cancelled_invoices()
    {
        var invoice = new Invoice { Number = "INV-1", Lines = [new InvoiceLine { Description = "Tuition", Amount = 1000 }] };
        Assert.True(invoice.Publish(DateTimeOffset.UtcNow));
        Assert.False(invoice.Publish(DateTimeOffset.UtcNow));

        var cancelled = new Invoice { Number = "INV-2" };
        cancelled.Cancel("Billed in error", DateTimeOffset.UtcNow);
        Assert.False(cancelled.Publish(DateTimeOffset.UtcNow));
        Assert.False(cancelled.IsPublished);
    }
}

public sealed class OptionalItemTests
{
    [Fact]
    public void Off_by_default_items_charge_only_students_ticked_on_by_default_everyone_except_those_unticked()
    {
        Guid ticked = Guid.NewGuid(), other = Guid.NewGuid();
        var bus = new FeeItem { Name = "Bus", IsMandatory = false, OptIns = [new FeeOptIn { StudentId = ticked }] };
        var books = new FeeItem { Name = "Books", IsMandatory = false, ChargedByDefault = true, OptIns = [new FeeOptIn { StudentId = ticked }] };

        Assert.Equal((true, false), (bus.Charges(ticked), bus.Charges(other)));
        Assert.Equal((false, true), (books.Charges(ticked), books.Charges(other)));
        Assert.True(new FeeItem { Name = "Tuition", IsMandatory = true }.Charges(other));
    }
}
