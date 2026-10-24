using Maydel.Application.Abstractions;
using Maydel.Application.Notifications;
using Maydel.Domain.Attendance;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maydel.Application.Finance;

/// <summary>
/// Confirms a Paystack payment and records it exactly once (E10.5). Used by the webhook and by the parent's return page;
/// whichever arrives first records it, the other sees it done. Everything is re-verified with Paystack: status "success",
/// currency NGN and the exact amount we asked for.
/// </summary>
public sealed class OnlinePayments(IAppDbContext db, IPaymentGateway gateway, TimeProvider time, FeeNotices notices, ILogger<OnlinePayments> logger)
{
    public async Task<OnlinePaymentResult?> ConfirmAsync(string reference, CancellationToken ct)
    {
        var attempt = await db.OnlinePayments.FirstOrDefaultAsync(o => o.Reference == reference, ct);
        if (attempt is null) return null;
        if (attempt.Status != OnlinePaymentStatus.Pending) return await ResultAsync(attempt, ct);

        var tx = await gateway.VerifyAsync(reference, ct);
        if (tx is null || tx.Status is "ongoing" or "pending" or "processing" or "queued") return await ResultAsync(attempt, ct);
        var now = time.GetUtcNow();
        if (tx.Status != "success")
        {
            (attempt.Status, attempt.Message, attempt.CompletedAt) = (OnlinePaymentStatus.Failed, Trim(tx.Message ?? tx.Status), now);
            await db.SaveChangesAsync(ct);
            return await ResultAsync(attempt, ct);
        }
        if (tx.Currency != "NGN" || tx.AmountKobo != OnlinePayment.ToKobo(attempt.Amount))
        {
            logger.LogWarning("Paystack {Reference}: paid {Kobo} {Currency}, expected {Expected} NGN", reference, tx.AmountKobo, tx.Currency, OnlinePayment.ToKobo(attempt.Amount));
            (attempt.Status, attempt.Message, attempt.CompletedAt) = (OnlinePaymentStatus.NeedsReview,
                $"Paystack reports {tx.AmountKobo / 100m:N2} {tx.Currency}; expected {attempt.Amount:N2} NGN. Not applied.", now);
            await db.SaveChangesAsync(ct);
            return await ResultAsync(attempt, ct);
        }

        var invoice = await db.Invoices.Include(i => i.Payments).Include(i => i.Term).ThenInclude(t => t.Session).FirstAsync(i => i.Id == attempt.InvoiceId, ct);
        var prefix = FeeRules.ReceiptNumber(invoice.Term.Session.Name, 0)[..^5];
        var number = FeeRules.ReceiptNumber(invoice.Term.Session.Name, await db.NextReceiptAsync(prefix, ct));
        var paidOn = RegisterRules.TodayAt(tx.PaidAt ?? now);
        var (payment, applied) = invoice.ApplyGatewayPayment(attempt.Amount, reference, paidOn, number);
        if (payment is not null)
        {
            db.Payments.Add(payment);
            await notices.PaymentAsync(payment, invoice, now, email: true, ct);
        }
        attempt.PaymentId = payment?.Id;
        attempt.Excess = attempt.Amount - applied;
        attempt.Status = attempt.Excess > 0 ? OnlinePaymentStatus.NeedsReview : OnlinePaymentStatus.Succeeded;
        attempt.Message = attempt.Excess > 0 ? $"NGN {attempt.Excess:N2} received after the invoice was settled: refund or reallocate it." : null;
        attempt.CompletedAt = now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The other path (webhook or return page) recorded it a moment earlier: show that result instead.
            return null;
        }
        return await ResultAsync(attempt, ct);
    }

    public async Task<OnlinePaymentResult> ResultAsync(OnlinePayment a, CancellationToken ct)
    {
        var receipt = a.PaymentId is { } id ? await db.Payments.Where(p => p.Id == id).Select(p => p.ReceiptNumber).FirstOrDefaultAsync(ct) : null;
        return new OnlinePaymentResult(a.Reference, a.Status, a.Amount, a.InvoiceId, a.StudentId, receipt, a.PaymentId, a.Excess, a.Message);
    }

    private static string Trim(string s) => s.Length > 300 ? s[..300] : s;
}
