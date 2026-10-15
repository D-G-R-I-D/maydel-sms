using Maydel.Domain.Finance;

namespace Maydel.Domain.Tests.Finance;

public sealed class InvoiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static Invoice Invoice(decimal total = 100000) =>
        new() { Number = "INV-1", Lines = [new InvoiceLine { Description = "Tuition", Amount = total }] };

    [Fact]
    public void Without_instalments_the_full_balance_is_due()
    {
        var invoice = Invoice();
        Assert.Equal("payment.full_required", invoice.RecordPayment(60000, PaymentMethod.Cash, null, Today, null, "R1", []).Error?.Code);

        var (payment, error) = invoice.RecordPayment(100000, PaymentMethod.BankTransfer, " TRF-123 ", Today, null, "R1", []);

        Assert.Null(error);
        Assert.Equal(("TRF-123", InvoiceStatus.Paid, 0m), (payment!.Reference, invoice.Status, invoice.Balance));
    }

    private static readonly decimal[] SixtyForty = [60000, 40000];

    [Theory]
    [InlineData(0, "payment.amount")]
    [InlineData(10.001, "payment.amount")]
    [InlineData(100000.01, "payment.exceeds_balance")]
    [InlineData(40000, "payment.instalment_minimum")]   // less than the first instalment (60%)
    [InlineData(60000, null)]
    [InlineData(80000, null)]                            // more than an instalment is fine
    [InlineData(100000, null)]
    public void With_instalments_the_first_payment_covers_at_least_the_first_instalment(decimal amount, string? code) =>
        Assert.Equal(code, Invoice().RecordPayment(amount, PaymentMethod.Pos, null, Today, null, "R1", SixtyForty).Error?.Code);

    [Fact]
    public void Each_later_payment_completes_the_next_instalment()
    {
        var invoice = Invoice();
        decimal[] plan = [50000, 30000, 20000];
        invoice.RecordPayment(55000, PaymentMethod.Cash, null, Today, null, "R1", plan);

        Assert.Equal(25000, FeeRules.NextInstalmentMinimum(plan, invoice.AmountPaid)); // up to the 80% mark
        Assert.Equal("payment.instalment_minimum", invoice.RecordPayment(20000, PaymentMethod.Cash, null, Today, null, "R2", plan).Error?.Code);
        Assert.Null(invoice.RecordPayment(25000, PaymentMethod.Cash, null, Today, null, "R2", plan).Error);
        Assert.Null(invoice.RecordPayment(20000, PaymentMethod.Cash, null, Today, null, "R3", plan).Error);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void Reversals_are_opposite_entries_once_per_payment()
    {
        var invoice = Invoice();
        var (payment, _) = invoice.RecordPayment(40000, PaymentMethod.Cash, null, Today, null, "R1", [40000, 60000]);

        Assert.Equal("payment.reason", invoice.Reverse(payment!, " ", Today, null, "R2").Error?.Code);
        var (reversal, error) = invoice.Reverse(payment!, "Keyed twice", Today, null, "R2");

        Assert.Null(error);
        Assert.Equal((-40000m, "R1", true), (reversal!.Amount, reversal.Reference, reversal.IsReversal));
        Assert.Equal((100000m, InvoiceStatus.Unpaid), (invoice.Balance, invoice.Status));
        Assert.Equal("payment.already_reversed", invoice.Reverse(payment!, "Again", Today, null, "R3").Error?.Code);
        Assert.Equal("payment.is_reversal", invoice.Reverse(reversal, "Undo the undo", Today, null, "R3").Error?.Code);
    }

    [Fact]
    public void Invoices_with_money_on_them_cannot_be_cancelled()
    {
        var invoice = Invoice();
        var (payment, _) = invoice.RecordPayment(40000, PaymentMethod.Cash, null, Today, null, "R1", [40000, 60000]);
        Assert.Equal("invoice.has_payments", invoice.Cancel("Left school", DateTimeOffset.UtcNow)?.Code);

        invoice.Reverse(payment!, "Refunded", Today, null, "R2");
        Assert.Null(invoice.Cancel("Left school", DateTimeOffset.UtcNow));
        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal("invoice.cancelled", invoice.RecordPayment(1, PaymentMethod.Cash, null, Today, null, "R3", [40000, 60000]).Error?.Code);
    }

    [Fact]
    public void Adjustments_never_take_the_total_below_what_is_paid()
    {
        var invoice = Invoice();
        invoice.RecordPayment(70000, PaymentMethod.Cash, null, Today, null, "R1", SixtyForty);

        Assert.Null(invoice.Adjust("Scholarship (25%)", -25000));
        Assert.Equal(75000, invoice.Total);
        Assert.Equal("invoice.adjustment_below_paid", invoice.Adjust("Scholarship", -10000)?.Code);
        Assert.Equal("invoice.adjustment_amount", invoice.Adjust("Nothing", 0)?.Code);
    }

    [Fact]
    public void Amount_due_by_a_date_follows_the_plan()
    {
        (decimal, DateOnly)[] plan = [(60000, new(2026, 9, 14)), (40000, new(2026, 11, 2))];
        Assert.Equal(60000, FeeRules.DueBy(Today, 100000, new(2026, 9, 14), plan));
        Assert.Equal(100000, FeeRules.DueBy(new(2026, 11, 2), 100000, new(2026, 9, 14), plan));
        Assert.Equal(100000, FeeRules.DueBy(Today, 100000, new(2026, 9, 14), []));
        Assert.Equal(0, FeeRules.DueBy(Today, 100000, new(2026, 12, 1), []));
    }

    [Fact]
    public void Receipt_numbers_carry_the_session() => Assert.Equal("RCT-2026-2027-00012", FeeRules.ReceiptNumber("2026/2027", 12));
}
