using System.Globalization;
using Maydel.Application.Abstractions;
using Maydel.Domain.Communication;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Notifications;

/// <summary>Queues each guardian's copy of a newly published invoice (E10.7); the PDF is attached at send time.</summary>
public sealed class InvoiceEmails(IAppDbContext db, IOptions<EmailOptions> options)
{
    public async Task<int> QueueAsync(IReadOnlyCollection<Invoice> invoices, DateTimeOffset now, CancellationToken ct)
    {
        if (invoices.Count == 0) return 0;
        var studentIds = invoices.Select(i => i.StudentId).Distinct().ToList();
        var termIds = invoices.Select(i => i.TermId).Distinct().ToList();
        var recipients = await db.StudentGuardians.AsNoTracking()
            .Where(g => studentIds.Contains(g.StudentId) && g.Guardian.Email != null && g.Guardian.Email != "")
            .Select(g => new { g.StudentId, g.GuardianId, g.Guardian.FullName, Email = g.Guardian.Email!, Child = g.Student.FirstName, ChildFull = g.Student.FirstName + " " + g.Student.LastName, g.Student.AdmissionNumber })
            .ToListAsync(ct);
        var terms = await db.Terms.AsNoTracking().Where(t => termIds.Contains(t.Id))
            .Select(t => new { t.Id, Term = t.Number.ToString() + " Term", Session = t.Session.Name }).ToDictionaryAsync(t => t.Id, ct);
        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        var schoolName = school?.Name ?? options.Value.FromName;

        var count = 0;
        foreach (var invoice in invoices)
        {
            var term = terms[invoice.TermId];
            foreach (var r in recipients.Where(r => r.StudentId == invoice.StudentId))
            {
                db.OutboxEmails.Add(new OutboxEmail
                {
                    Kind = EmailKind.InvoicePublished,
                    ToAddress = r.Email.Trim(),
                    ToName = r.FullName,
                    Subject = $"{r.Child}'s {term.Term} fees ({term.Session}) – {schoolName}",
                    Body = Body(r.FullName, r.ChildFull, r.AdmissionNumber, term.Term, term.Session, invoice, schoolName, school?.Phone, school?.Email, options.Value.PortalUrl),
                    StudentId = invoice.StudentId, TermId = invoice.TermId, GuardianId = r.GuardianId, InvoiceId = invoice.Id,
                    NextAttemptAt = now,
                });
                count++;
            }
        }
        return count;
    }

    private static string Body(string guardian, string child, string admissionNumber, string term, string session, Invoice invoice,
        string school, string? phone, string? email, string? portal)
    {
        var naira = CultureInfo.InvariantCulture;
        var lines = new List<string>
        {
            $"Dear {guardian},",
            "",
            $"{child}'s fees invoice for the {term}, {session} session, is ready. The invoice is attached as a PDF.",
            "",
            $"Invoice: {invoice.Number}",
            $"Total: NGN {invoice.Total.ToString("#,##0.00", naira)}",
        };
        if (invoice.DueOn is { } due) lines.Add($"Due by: {due.ToString("d MMMM yyyy", naira)}");
        lines.Add("");
        lines.Add($"When paying by bank transfer or at the bank, please quote the admission number {admissionNumber} as the reference.");
        if (!string.IsNullOrWhiteSpace(portal))
        {
            lines.Add("");
            lines.Add($"You can see the invoice, pay online and download receipts in the parent portal: {portal.TrimEnd('/')}");
        }
        lines.Add("");
        lines.Add(school);
        var contact = string.Join(" · ", new[] { phone, email }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (contact.Length > 0) lines.Add(contact);
        return string.Join("\n", lines);
    }
}
