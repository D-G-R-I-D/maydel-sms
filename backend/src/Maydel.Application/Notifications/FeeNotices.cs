using System.Globalization;
using Maydel.Application.Abstractions;
using Maydel.Domain.Communication;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Notifications;

/// <summary>E12.8: tells families about every payment (portal notice + receipt by email) and about overdue fees.</summary>
public sealed class FeeNotices(IAppDbContext db, Notifier notifier, IOptions<EmailOptions> options)
{
    /// <summary>Where the parent portal shows invoices, payments and receipts.</summary>
    public const string PortalFees = "/portal/fees";

    /// <summary>
    /// A payment has been recorded on <paramref name="invoice"/> (its Payments must include it). Guardians with a login get a
    /// notice; guardians with an email get the receipt, unless <paramref name="email"/> is false (a confirmed proof already sends it).
    /// </summary>
    public async Task PaymentAsync(Payment payment, Invoice invoice, DateTimeOffset now, bool email, CancellationToken ct)
    {
        var child = await ChildAsync(invoice.StudentId, ct);
        var balance = invoice.Balance;
        var balanceLine = balance > 0 ? $"Balance on invoice {invoice.Number}: NGN {Naira(balance)}." : $"Invoice {invoice.Number} is now fully paid.";
        await notifier.GuardiansAsync(invoice.StudentId, NotificationKind.PaymentReceived, $"Payment received for {child.FirstName}",
            $"NGN {Naira(payment.Amount)} received on {payment.PaidOn.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} (receipt {payment.ReceiptNumber}). {balanceLine}",
            PortalFees, $"payment:{payment.Id}", ct);
        if (!email) return;

        var school = await SchoolAsync(ct);
        foreach (var r in await RecipientsAsync(invoice.StudentId, ct))
        {
            db.OutboxEmails.Add(new OutboxEmail
            {
                Kind = EmailKind.PaymentReceipt,
                ToAddress = r.Email.Trim(), ToName = r.FullName,
                Subject = $"Payment received – {child.FullName} ({payment.ReceiptNumber})",
                Body = Lines(
                    $"Dear {r.FullName},", "",
                    $"Thank you. We've received a payment for {child.FullName} ({child.AdmissionNumber}). Your receipt is attached.", "",
                    $"Amount: NGN {Naira(payment.Amount)}",
                    $"Receipt: {payment.ReceiptNumber}",
                    $"Paid on: {payment.PaidOn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}",
                    $"Invoice: {invoice.Number}",
                    balance > 0 ? $"Balance: NGN {Naira(balance)}" : "Balance: Fully paid", "",
                    PortalLine(), school),
                StudentId = invoice.StudentId, TermId = invoice.TermId, GuardianId = r.GuardianId, InvoiceId = invoice.Id, PaymentId = payment.Id,
                NextAttemptAt = now,
            });
        }
    }

    /// <summary>Reminds the family that <paramref name="overdue"/> of the invoice is past due. Returns the emails queued.</summary>
    public async Task<int> OverdueAsync(Invoice invoice, decimal overdue, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var child = await ChildAsync(invoice.StudentId, ct);
        var term = $"{invoice.Term.Number} Term {invoice.Term.Session.Name}";
        var summary = $"NGN {Naira(overdue)} of {child.FirstName}'s {term} fees (invoice {invoice.Number}) is overdue. Balance: NGN {Naira(invoice.Balance)}.";
        await notifier.GuardiansAsync(invoice.StudentId, NotificationKind.FeesOverdue, $"{child.FirstName}'s fees are overdue",
            $"{summary} Pay online or send proof of payment in the portal.", PortalFees, $"overdue:{invoice.Id}:{today:yyyy-MM-dd}", ct);

        var school = await SchoolAsync(ct);
        var recipients = await RecipientsAsync(invoice.StudentId, ct);
        foreach (var r in recipients)
        {
            db.OutboxEmails.Add(new OutboxEmail
            {
                Kind = EmailKind.FeesOverdue,
                ToAddress = r.Email.Trim(), ToName = r.FullName,
                Subject = $"Fees overdue – {child.FullName} ({invoice.Number})",
                Body = Lines(
                    $"Dear {r.FullName},", "",
                    $"{child.FirstName}'s {term} fees are overdue.", "",
                    $"Overdue: NGN {Naira(overdue)}",
                    $"Balance: NGN {Naira(invoice.Balance)}",
                    $"Invoice: {invoice.Number}",
                    $"Reference: {child.AdmissionNumber}", "",
                    "Please pay as soon as you can, quoting the reference above. If you've already paid, please send the proof of payment in the parent portal or contact the accounts office, and ignore this reminder.", "",
                    PortalLine(), school),
                StudentId = invoice.StudentId, TermId = invoice.TermId, GuardianId = r.GuardianId, InvoiceId = invoice.Id,
                NextAttemptAt = now,
            });
        }
        return recipients.Count;
    }

    private sealed record Child(string FirstName, string FullName, string AdmissionNumber);
    private sealed record Recipient(Guid GuardianId, string FullName, string Email);

    private Task<Child> ChildAsync(Guid studentId, CancellationToken ct) =>
        db.Students.AsNoTracking().Where(s => s.Id == studentId)
            .Select(s => new Child(s.FirstName, s.FirstName + " " + s.LastName, s.AdmissionNumber)).FirstAsync(ct);

    private Task<List<Recipient>> RecipientsAsync(Guid studentId, CancellationToken ct) =>
        db.StudentGuardians.AsNoTracking().Where(g => g.StudentId == studentId && g.Guardian.Email != null && g.Guardian.Email != "")
            .Select(g => new Recipient(g.GuardianId, g.Guardian.FullName, g.Guardian.Email!)).ToListAsync(ct);

    private async Task<string> SchoolAsync(CancellationToken ct) =>
        await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? options.Value.FromName;

    private string PortalLine() => string.IsNullOrWhiteSpace(options.Value.PortalUrl) ? ""
        : $"You can see invoices, pay online and download receipts in the parent portal: {options.Value.PortalUrl.TrimEnd('/')}\n";

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    private static string Naira(decimal amount) => amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
}
