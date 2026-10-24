using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Notifications;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ConfirmPaymentProof;

internal sealed class ConfirmPaymentProofHandler(IAppDbContext db, PaymentProofs proofs, ProofEmails emails, FeeNotices notices, ISchoolClock clock, TimeProvider time, ICurrentUser me)
    : ICommandHandler<ConfirmPaymentProofCommand, ProofDto>
{
    public async Task<Result<ProofDto>> Handle(ConfirmPaymentProofCommand c, CancellationToken ct)
    {
        var proof = await db.PaymentProofs.FirstOrDefaultAsync(p => p.Id == c.ProofId, ct);
        if (proof is null) return FinanceErrors.ProofNotFound;
        if (proof.Status != ProofStatus.Pending) return Error.Rule("proof.already_reviewed", "This proof of payment has already been reviewed.");
        var method = c.Method ?? PaymentMethod.BankTransfer;
        if (method is not (PaymentMethod.BankTransfer or PaymentMethod.Cash or PaymentMethod.Pos)) return Error.Validation("method", "Choose bank transfer, cash or POS.");
        var paidOn = c.PaidOn ?? proof.PaidOn;
        if (paidOn > clock.Today) return FinanceErrors.FuturePayment;

        var invoice = await db.Invoices.Include(i => i.Payments).Include(i => i.Term).ThenInclude(t => t.Session).FirstAsync(i => i.Id == proof.InvoiceId, ct);
        var prefix = FeeRules.ReceiptNumber(invoice.Term.Session.Name, 0)[..^5];
        var number = FeeRules.ReceiptNumber(invoice.Term.Session.Name, await db.NextReceiptAsync(prefix, ct));
        var (payment, error) = invoice.RecordVerifiedPayment(c.Amount ?? proof.Amount, method, proof.BankReference, paidOn, me.UserId, number);
        if (error is not null) return error.ToError();
        db.Payments.Add(payment!);
        proof.Confirm(payment!.Id, me.UserId, time.GetUtcNow());
        await emails.QueueAsync(proof, confirmed: true, time.GetUtcNow(), ct);
        // The confirmation email above already carries the receipt: portal notice only.
        await notices.PaymentAsync(payment!, invoice, time.GetUtcNow(), email: false, ct);
        await db.SaveChangesAsync(ct);
        return await proofs.Dtos(db.PaymentProofs.Where(p => p.Id == proof.Id)).FirstAsync(ct);
    }
}
