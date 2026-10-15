using Maydel.Application.Abstractions;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Notifications;

/// <summary>
/// Queues each guardian's copy of a newly published report (E12.3 trigger "result published"). Only guardians linked to
/// the student with an email on record receive it. Publishing again supersedes anything not yet sent for that report.
/// </summary>
public sealed class ReportEmails(IAppDbContext db, IOptions<EmailOptions> options)
{
    public async Task<int> QueueAsync(IReadOnlyCollection<Guid> studentIds, Guid termId, DateTimeOffset now, CancellationToken ct)
    {
        if (studentIds.Count == 0) return 0;

        var superseded = await db.OutboxEmails
            .Where(e => e.Kind == EmailKind.ReportPublished && e.Status == EmailStatus.Pending && e.TermId == termId
                && e.StudentId != null && studentIds.Contains(e.StudentId.Value))
            .ToListAsync(ct);
        foreach (var e in superseded) e.MarkSkipped("Superseded: the report was published again.");

        var recipients = await db.StudentGuardians.AsNoTracking()
            .Where(g => studentIds.Contains(g.StudentId) && g.Guardian.Email != null && g.Guardian.Email != "")
            .Select(g => new { g.StudentId, g.GuardianId, g.Guardian.FullName, Email = g.Guardian.Email!, Child = g.Student.FirstName, ChildFull = g.Student.FirstName + " " + g.Student.LastName })
            .ToListAsync(ct);
        var term = await db.Terms.AsNoTracking().Where(t => t.Id == termId)
            .Select(t => new { Term = t.Number.ToString() + " Term", Session = t.Session.Name }).FirstAsync(ct);
        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        var schoolName = school?.Name ?? options.Value.FromName;

        foreach (var r in recipients)
        {
            db.OutboxEmails.Add(new OutboxEmail
            {
                Kind = EmailKind.ReportPublished,
                ToAddress = r.Email.Trim(),
                ToName = r.FullName,
                Subject = $"{r.Child}'s {term.Term} report ({term.Session}) – {schoolName}",
                Body = Body(r.FullName, r.ChildFull, term.Term, term.Session, schoolName, school?.Phone, school?.Email, options.Value.PortalUrl),
                StudentId = r.StudentId,
                TermId = termId,
                GuardianId = r.GuardianId,
                NextAttemptAt = now,
            });
        }
        return recipients.Count;
    }

    private static string Body(string guardian, string child, string term, string session, string school, string? phone, string? email, string? portal)
    {
        var lines = new List<string>
        {
            $"Dear {guardian},",
            "",
            $"{child}'s {term} report for the {session} session has been published. The report card is attached as a PDF.",
        };
        if (!string.IsNullOrWhiteSpace(portal))
        {
            lines.Add("");
            lines.Add($"You can also view it, and earlier reports, in the parent portal: {portal.TrimEnd('/')}");
        }
        lines.Add("");
        lines.Add("If you have any questions about the report, please contact the school.");
        lines.Add("");
        lines.Add(school);
        var contact = string.Join(" · ", new[] { phone, email }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (contact.Length > 0) lines.Add(contact);
        return string.Join("\n", lines);
    }
}
