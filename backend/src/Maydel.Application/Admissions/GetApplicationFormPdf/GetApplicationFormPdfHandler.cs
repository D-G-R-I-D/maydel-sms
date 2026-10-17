using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Application.Reports;
using Maydel.Application.SchoolSettings;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.GetApplicationFormPdf;

internal sealed class GetApplicationFormPdfHandler(
    IAppDbContext db, SchoolBranding branding, IAdmissionFormRenderer renderer, PaymentProofs files, ISchoolClock clock, ICurrentUser me)
    : IQueryHandler<GetApplicationFormPdfQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetApplicationFormPdfQuery q, CancellationToken ct)
    {
        if (q.ApplicationId is not { } id)
        {
            var name = await db.Sessions.AsNoTracking().Where(s => s.Id == q.SessionId).Select(s => s.Name).FirstOrDefaultAsync(ct);
            if (name is null) return AdmissionErrors.SessionNotFound;
            var blank = renderer.Render(new AdmissionFormDocument(null, name, null, clock.Today), await branding.LoadAsync(ct));
            return new PdfFile(blank, ReportPdfs.Safe($"Admission form {name}") + ".pdf");
        }

        var application = await db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && (q.AsStaff || a.ApplicantUserId == me.UserId), ct);
        if (application is null || await db.DetailAsync(id, ct) is not { } detail) return AdmissionErrors.ApplicationNotFound;
        byte[]? photo = null;
        if (application.Documents.FirstOrDefault(d => d.Kind == DocumentKind.PassportPhoto) is { } passport
            && await files.OpenAsync(passport.FilePath, passport.ContentType, ct) is { } content)
        {
            await using var stream = content.Content;
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            photo = buffer.ToArray();
        }
        var bytes = renderer.Render(new AdmissionFormDocument(detail, detail.Application.Session, photo, clock.Today), await branding.LoadAsync(ct));
        return new PdfFile(bytes, ReportPdfs.Safe($"{application.ReferenceNumber} - {application.ChildName}") + ".pdf");
    }
}
