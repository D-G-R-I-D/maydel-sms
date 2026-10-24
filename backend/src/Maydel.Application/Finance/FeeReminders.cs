using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Notifications;
using Maydel.Domain.Attendance;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance;

public sealed record FeeReminderSettings(bool Enabled, int IntervalDays);

public sealed record FeeReminderResult(int Invoices, int Emails);

public sealed record GetFeeReminderSettingsQuery : IQuery<FeeReminderSettings>;

/// <summary>Switches overdue reminders on or off and sets how often they repeat (admin).</summary>
public sealed record SaveFeeReminderSettingsCommand(bool Enabled, int IntervalDays) : ICommand<FeeReminderSettings>;

/// <summary>
/// Reminds families whose published invoices have an overdue amount (by instalment plan or due date), once, then every
/// interval while it stays overdue (E12.8). Runs from a background worker during office hours (08:00–18:00 WAT).
/// </summary>
public sealed record SendFeeRemindersCommand(DateTimeOffset? Now = null, Guid? TermId = null) : ICommand<FeeReminderResult>;

internal sealed class GetFeeReminderSettingsHandler(IAppDbContext db) : IQueryHandler<GetFeeReminderSettingsQuery, FeeReminderSettings>
{
    public async Task<Result<FeeReminderSettings>> Handle(GetFeeReminderSettingsQuery q, CancellationToken ct)
    {
        var s = await db.SettingsAsync(ct);
        return new FeeReminderSettings(s.RemindersEnabled, s.ReminderIntervalDays);
    }
}

internal sealed class SaveFeeReminderSettingsHandler(IAppDbContext db) : ICommandHandler<SaveFeeReminderSettingsCommand, FeeReminderSettings>
{
    public async Task<Result<FeeReminderSettings>> Handle(SaveFeeReminderSettingsCommand c, CancellationToken ct)
    {
        if (FeeSettings.CheckReminders(c.IntervalDays) is { } invalid) return invalid.ToError();
        var s = await db.FeeSettings.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (s is null) db.FeeSettings.Add(s = new FeeSettings());
        (s.RemindersEnabled, s.ReminderIntervalDays) = (c.Enabled, c.IntervalDays);
        await db.SaveChangesAsync(ct);
        return new FeeReminderSettings(s.RemindersEnabled, s.ReminderIntervalDays);
    }
}

internal sealed class SendFeeRemindersHandler(IAppDbContext db, FeeNotices notices, ISchoolClock clock) : ICommandHandler<SendFeeRemindersCommand, FeeReminderResult>
{
    public async Task<Result<FeeReminderResult>> Handle(SendFeeRemindersCommand c, CancellationToken ct)
    {
        var now = (c.Now ?? clock.Now).ToUniversalTime();
        var hour = now.ToOffset(RegisterRules.SchoolUtcOffset).Hour;
        var settings = await db.SettingsAsync(ct);
        if (hour is < 8 or >= 18 || !settings.RemindersEnabled) return new FeeReminderResult(0, 0);

        var today = RegisterRules.TodayAt(now);
        var remindedBefore = today.AddDays(-settings.ReminderIntervalDays);
        var invoices = await db.Invoices.Include(i => i.Payments).Include(i => i.Term).ThenInclude(t => t.Session)
            .Where(i => !i.IsCancelled && i.PublishedAt != null && (c.TermId == null || i.TermId == c.TermId)
                && (i.LastReminderOn == null || i.LastReminderOn <= remindedBefore)
                && i.Lines.Sum(l => l.Amount) > i.Payments.Sum(p => p.Amount))
            .ToListAsync(ct);

        var plans = new Dictionary<Guid, List<Instalment>>();
        var (count, emails) = (0, 0);
        foreach (var invoice in invoices)
        {
            if (!plans.TryGetValue(invoice.TermId, out var plan))
                plans[invoice.TermId] = plan = await db.PlanForAsync(invoice.Term, ct) is { Plan.Enabled: true } p ? [.. p.Plan.Instalments.OrderBy(x => x.Order)] : [];
            var amounts = plan.Count == 0 ? [] : FeeRules.Split(invoice.Total, [.. plan.Select(x => x.Percent)]);
            var overdue = FeeRules.DueBy(today, invoice.Total, invoice.DueOn, [.. plan.Select((x, n) => (amounts[n], x.DueOn))]) - invoice.AmountPaid;
            if (overdue <= 0) continue;
            emails += await notices.OverdueAsync(invoice, overdue, today, clock.Now.ToUniversalTime(), ct); // queued now, even when sweeping as of another time
            invoice.LastReminderOn = today;
            count++;
        }
        await db.SaveChangesAsync(ct);
        return new FeeReminderResult(count, emails);
    }
}
