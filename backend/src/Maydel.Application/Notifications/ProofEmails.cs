using System.Globalization;
using Maydel.Application.Abstractions;
using Maydel.Domain.Communication;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Notifications;

/// <summary>Tells the parent who sent a proof of payment (or, failing that, the child's guardians) how it was reviewed.</summary>
public sealed class ProofEmails(IAppDbContext db, IOptions<EmailOptions> options)
{
    public async Task<int> QueueAsync(PaymentProof proof, bool confirmed, DateTimeOffset now, CancellationToken ct)
    {
        var sender = await db.Guardians.AsNoTracking().Where(g => g.UserId == proof.SubmittedByUserId && g.Email != null && g.Email != "")
            .Select(g => new { g.Id, g.FullName, Email = g.Email! }).ToListAsync(ct);
        var recipients = sender.Count > 0 ? sender : await db.StudentGuardians.AsNoTracking()
            .Where(g => g.StudentId == proof.StudentId && g.Guardian.Email != null && g.Guardian.Email != "")
            .Select(g => new { Id = g.GuardianId, g.Guardian.FullName, Email = g.Guardian.Email! }).ToListAsync(ct);
        var info = await db.Invoices.AsNoTracking().Where(i => i.Id == proof.InvoiceId)
            .Select(i => new { i.Number, Child = i.Student.FirstName + " " + i.Student.LastName, i.Student.AdmissionNumber }).FirstAsync(ct);
        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? options.Value.FromName;
        var amount = proof.Amount.ToString("#,##0.00", CultureInfo.InvariantCulture);

        foreach (var r in recipients)
        {
            var body = confirmed
                ? $"Dear {r.FullName},\n\nThank you. We've confirmed your payment of NGN {amount} for {info.Child} ({info.AdmissionNumber}) against invoice {info.Number}. Your receipt is attached.\n\n{school}"
                : $"Dear {r.FullName},\n\nWe couldn't confirm the proof of payment of NGN {amount} you sent for {info.Child} ({info.AdmissionNumber}) against invoice {info.Number}.\n\nReason: {proof.RejectReason}\n\nPlease check and send it again from the parent portal, or contact the school's accounts office.\n\n{school}";
            db.OutboxEmails.Add(new OutboxEmail
            {
                Kind = confirmed ? EmailKind.ProofConfirmed : EmailKind.ProofRejected,
                ToAddress = r.Email.Trim(), ToName = r.FullName,
                Subject = confirmed ? $"Payment confirmed – {info.Child} ({info.Number})" : $"Proof of payment not confirmed – {info.Child} ({info.Number})",
                Body = body, StudentId = proof.StudentId, GuardianId = r.Id, InvoiceId = proof.InvoiceId, PaymentId = proof.PaymentId, NextAttemptAt = now,
            });
        }
        return recipients.Count;
    }
}
