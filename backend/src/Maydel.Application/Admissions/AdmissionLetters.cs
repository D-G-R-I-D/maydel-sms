using Maydel.Application.Abstractions;
using Maydel.Application.Reports;
using Maydel.Application.SchoolSettings;
using Maydel.Domain.Admissions;
using Maydel.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions;

/// <summary>Builds the admission letter PDF (E11.8) while the offer stands (offered, accepted or enrolled).</summary>
public sealed class AdmissionLetters(IAppDbContext db, SchoolBranding branding, IAdmissionLetterRenderer renderer)
{
    public async Task<PdfFile?> LetterAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == applicationId
            && (a.Status == ApplicationStatus.Offered || a.Status == ApplicationStatus.Accepted || a.Status == ApplicationStatus.Enrolled), ct);
        if (app is not { AdmissionNumber: { } number, DecidedAt: { } decided }) return null;
        var session = await db.Sessions.AsNoTracking().Where(s => s.Id == app.SessionId).Select(s => new { s.Name, s.StartsOn }).FirstAsync(ct);
        var level = await db.ClassLevels.AsNoTracking().Where(l => l.Id == app.ClassLevelId).Select(l => l.Name).FirstAsync(ct);
        var resumes = await db.Terms.AsNoTracking().Where(t => t.SessionId == app.SessionId).OrderBy(t => t.StartsOn).Select(t => (DateOnly?)t.StartsOn).FirstOrDefaultAsync(ct)
            ?? session.StartsOn;
        var name = string.Join(' ', new[] { app.ChildFirstName, app.Form.ChildMiddleName, app.ChildLastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var letter = new AdmissionLetterDocument(app.ReferenceNumber, number, name, level, session.Name, app.ParentName, app.Form.HomeAddress,
            RegisterRules.TodayAt(decided), resumes);
        return new PdfFile(renderer.Render(letter, await branding.LoadAsync(ct)), ReportPdfs.Safe($"Admission letter - {name}") + ".pdf");
    }
}
