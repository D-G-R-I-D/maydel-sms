using Maydel.Application.Abstractions;
using Maydel.Domain.Admissions;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Admissions;

/// <summary>Queues admissions stage emails (E11.9) to the family through the outbox: greeting, message, portal link, school name.</summary>
public sealed class AdmissionEmails(IAppDbContext db, TimeProvider time, IOptions<EmailOptions> options)
{
    private string? _school;

    /// <summary>"Thursday 22 October 2026, 9:00 AM" in Abuja time.</summary>
    public static string When(DateTimeOffset at) =>
        at.ToOffset(Domain.Attendance.RegisterRules.SchoolUtcOffset).ToString("dddd d MMMM yyyy, h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

    public async Task QueueAsync(AdmissionApplication application, string subject, string message, CancellationToken ct, EmailKind kind = EmailKind.AdmissionStage)
    {
        _school ??= await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? options.Value.FromName;
        var portal = options.Value.PortalUrl is { } url ? $"\n\nYour application: {url.TrimEnd('/')}/admissions" : "";
        db.OutboxEmails.Add(new OutboxEmail
        {
            Kind = kind, ApplicationId = application.Id, ToAddress = application.Email, ToName = application.ParentName,
            Subject = $"{subject} – {application.ChildName} ({application.ReferenceNumber})",
            Body = $"Dear {application.ParentName},\n\n{message}{portal}\n\n{_school}",
            NextAttemptAt = time.GetUtcNow(),
        });
    }
}
