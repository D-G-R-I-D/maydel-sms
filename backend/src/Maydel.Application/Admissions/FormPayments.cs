using Maydel.Application.Abstractions;
using Maydel.Domain.Admissions;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions;

/// <summary>Confirms an online form payment (same rules as fees: re-verify status, NGN and exact amount; once only).</summary>
public sealed class FormPayments(IAppDbContext db, IPaymentGateway gateway, FormPaid paid, TimeProvider time)
{
    public sealed record Outcome(string Reference, FormPaymentStatus Status, Guid ApplicationId, string? Message);

    public async Task<Outcome?> ConfirmAsync(string reference, CancellationToken ct)
    {
        var attempt = await db.FormPayments.FirstOrDefaultAsync(f => f.Reference == reference, ct);
        if (attempt is null) return null;
        if (attempt.Status != FormPaymentStatus.Pending) return Result(attempt);

        var tx = await gateway.VerifyAsync(reference, ct);
        if (tx is null || tx.Status is "ongoing" or "pending" or "processing" or "queued") return Result(attempt);
        var now = time.GetUtcNow();
        if (tx.Status != "success") (attempt.Status, attempt.Message) = (FormPaymentStatus.Failed, tx.Message ?? tx.Status);
        else if (tx.Currency != "NGN" || tx.AmountKobo != OnlinePayment.ToKobo(attempt.Amount))
            (attempt.Status, attempt.Message) = (FormPaymentStatus.NeedsReview, $"Paystack reports {tx.AmountKobo / 100m:N2} {tx.Currency}; expected {attempt.Amount:N2} NGN.");
        else
        {
            var application = await db.Applications.FirstAsync(a => a.Id == attempt.ApplicationId, ct);
            attempt.Status = await paid.ApplyAsync(application, FormPaymentMethod.Online, null, ct) is null ? FormPaymentStatus.Succeeded : FormPaymentStatus.NeedsReview;
            if (attempt.Status == FormPaymentStatus.NeedsReview) attempt.Message = "Paid again for a form that was already paid for: refund it.";
        }
        attempt.CompletedAt = now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return null; }
        return Result(attempt);
    }

    private static Outcome Result(FormPayment f) => new(f.Reference, f.Status, f.ApplicationId, f.Message);
}
