using Maydel.Application.Abstractions;
using Maydel.Domain.Admissions;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Admissions;

/// <summary>Marks a form paid (code or online) and queues the "your form is ready" email (E11.9).</summary>
public sealed class FormPaid(IAppDbContext db, ISchoolClock clock, TimeProvider time, IOptions<EmailOptions> options)
{
    public async Task<Domain.Common.DomainError?> ApplyAsync(AdmissionApplication application, FormPaymentMethod method, string? code, CancellationToken ct)
    {
        var days = await db.AdmissionSettings.Where(s => s.SessionId == application.SessionId).Select(s => (int?)s.ExamWindowDays).FirstOrDefaultAsync(ct) ?? 5;
        var now = time.GetUtcNow();
        if (application.MarkFormPaid(method, code, now, clock.Today, days) is { } invalid) return invalid;

        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? options.Value.FromName;
        var portal = options.Value.PortalUrl is { } url ? $"\n\nContinue here: {url.TrimEnd('/')}/admissions" : "";
        db.OutboxEmails.Add(new OutboxEmail
        {
            Kind = EmailKind.AdmissionStage, ToAddress = application.Email, ToName = application.ParentName,
            Subject = $"Admission form ready – {application.ChildName} ({application.ReferenceNumber})",
            Body = $"Dear {application.ParentName},\n\nThank you for buying the admission form for {application.ChildName} (reference {application.ReferenceNumber}). " +
                   $"You can now fill the form online and book the entrance exam.\n\nPlease book and take the exam by {application.ExamWindowEndsOn:dddd, d MMMM yyyy}; after that the window closes.{portal}\n\n{school}",
            NextAttemptAt = now,
        });
        return null;
    }
}
