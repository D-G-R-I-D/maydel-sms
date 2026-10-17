using Maydel.Domain.Finance;

namespace Maydel.Domain.Tests.Finance;

public sealed class PaymentProofTests
{
    private static PaymentProof Proof() => new() { FilePath = "proofs/x.jpg", ContentType = "image/jpeg", Amount = 50000 };

    [Fact]
    public void A_proof_is_reviewed_once()
    {
        var proof = Proof();
        var payment = Guid.NewGuid();
        Assert.Null(proof.Confirm(payment, null, DateTimeOffset.UtcNow));
        Assert.Equal((ProofStatus.Confirmed, (Guid?)payment), (proof.Status, proof.PaymentId));
        Assert.Equal("proof.already_reviewed", proof.Reject("Too late", null, DateTimeOffset.UtcNow)?.Code);
    }

    [Fact]
    public void Rejections_need_a_reason()
    {
        var proof = Proof();
        Assert.Equal("proof.reason", proof.Reject("  ", null, DateTimeOffset.UtcNow)?.Code);
        Assert.Null(proof.Reject(" Teller is unreadable ", null, DateTimeOffset.UtcNow));
        Assert.Equal("Teller is unreadable", proof.RejectReason);
    }

    [Fact]
    public void Verified_bank_payments_skip_the_instalment_minimum_but_not_the_balance()
    {
        var invoice = new Invoice { Number = "INV-1", Lines = [new InvoiceLine { Description = "Tuition", Amount = 100000 }] };
        Assert.Null(invoice.RecordVerifiedPayment(10000, PaymentMethod.BankTransfer, "TRF-1", new(2026, 10, 15), null, "R1").Error);
        Assert.Equal("payment.exceeds_balance", invoice.RecordVerifiedPayment(95000, PaymentMethod.BankTransfer, null, new(2026, 10, 15), null, "R2").Error?.Code);
    }
}
