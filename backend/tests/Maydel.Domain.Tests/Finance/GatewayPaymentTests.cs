using Maydel.Domain.Finance;

namespace Maydel.Domain.Tests.Finance;

public sealed class GatewayPaymentTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static Invoice Invoice() => new() { Number = "INV-1", Lines = [new InvoiceLine { Description = "Tuition", Amount = 100000 }] };

    [Fact]
    public void Gateway_money_is_applied_up_to_the_balance()
    {
        var invoice = Invoice();
        invoice.RecordPayment(70000, PaymentMethod.Cash, null, Today, null, "R1", [60000, 40000]);

        var (payment, applied) = invoice.ApplyGatewayPayment(60000, "MSS-abc", Today, "R2");

        Assert.Equal((30000m, PaymentMethod.Online, "MSS-abc"), (applied, payment!.Method, payment.Reference));
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void Nothing_is_applied_to_a_settled_or_cancelled_invoice()
    {
        var paid = Invoice();
        paid.RecordPayment(100000, PaymentMethod.Cash, null, Today, null, "R1", []);
        Assert.Equal(0, paid.ApplyGatewayPayment(5000, "MSS-x", Today, "R2").Applied);

        var cancelled = Invoice();
        cancelled.Cancel("Billed in error", DateTimeOffset.UtcNow);
        Assert.Null(cancelled.ApplyGatewayPayment(5000, "MSS-y", Today, "R2").Payment);
    }

    [Theory]
    [InlineData(1500.50, 150050)]
    [InlineData(0.01, 1)]
    public void Naira_converts_to_kobo(decimal naira, long kobo) => Assert.Equal(kobo, OnlinePayment.ToKobo(naira));
}
