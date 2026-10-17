using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Maydel.Domain.Attendance;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.SweepAdmissions;

internal sealed class SweepAdmissionsHandler(IAppDbContext db, ISchoolClock clock, AdmissionEmails emails, IFileStorage storage, UserManager<AppUser> users)
    : ICommandHandler<SweepAdmissionsCommand, SweepResult>
{
    public async Task<Result<SweepResult>> Handle(SweepAdmissionsCommand c, CancellationToken ct)
    {
        var now = (c.Now ?? clock.Now).ToUniversalTime();
        var today = RegisterRules.TodayAt(now);
        var apps = db.Applications.Where(a => c.SessionId == null || a.SessionId == c.SessionId);

        var overdue = await apps
            .Where(a => (a.Status == ApplicationStatus.FormPaid || a.Status == ApplicationStatus.Submitted) && a.ExamWindowEndsOn < today)
            .ToListAsync(ct);
        foreach (var application in overdue.Where(a => a.ExpireIfOverdue(now)))
            await emails.QueueAsync(application, "Entrance exam window closed",
                $"The window to take {application.ChildName}'s entrance exam closed on {application.ExamWindowEndsOn:d MMMM yyyy}. " +
                "If you'd still like to continue, please contact the school to reopen the application.", ct);

        var soon = now.AddHours(24);
        var due = await apps.Where(a => a.Status == ApplicationStatus.ExamBooked && a.ExamReminderSentAt == null)
            .Join(db.ExamSlots.Where(s => s.StartsAt > now && s.StartsAt <= soon), a => a.ExamSlotId, s => (Guid?)s.Id, (a, s) => new { a, s })
            .ToListAsync(ct);
        foreach (var (application, slot) in due.Select(x => (x.a, x.s)))
        {
            application.ExamReminderSentAt = now;
            await emails.QueueAsync(application, "Reminder: entrance exam",
                $"A reminder that {application.ChildName}'s entrance exam is on {AdmissionEmails.When(slot.StartsAt)} at {slot.Venue}. " +
                "Please arrive 15 minutes early.", ct);
        }
        // NDPR: unsuccessful applications keep no child data beyond 6 months.
        var cutoff = now.AddMonths(-AdmissionApplication.PurgeAfterMonths);
        var stale = await apps.Where(a => a.DataPurgedAt == null && (
                (a.Status == ApplicationStatus.NotOffered && a.DecidedAt <= cutoff) ||
                (a.Status == ApplicationStatus.Missed && a.MissedAt <= cutoff) ||
                (a.Status == ApplicationStatus.Withdrawn && (a.RespondedAt ?? a.UpdatedAt) <= cutoff)))
            .ToListAsync(ct);
        var purge = stale.Where(a => a.IsDueForPurge(now)).ToList();
        var files = purge.SelectMany(a => a.Purge(now)).ToList();

        // Applicant-only accounts (never became parents) unused for 12 months are switched off.
        var deactivated = 0;
        if (c.SessionId is null)
        {
            var idle = now.AddMonths(-12);
            foreach (var user in await users.GetUsersInRoleAsync(Roles.Applicant))
            {
                if (!user.IsActive || (user.LastLoginAt ?? user.CreatedAt) > idle || await users.IsInRoleAsync(user, Roles.Parent)) continue;
                user.IsActive = false;
                await users.UpdateAsync(user);
                await users.UpdateSecurityStampAsync(user);
                deactivated++;
            }
        }

        await db.SaveChangesAsync(ct);
        foreach (var file in files) await storage.DeleteAsync(file, ct);
        return new SweepResult(overdue.Count, due.Count, purge.Count, deactivated);
    }
}
