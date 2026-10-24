using System.Security.Cryptography;
using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Attendance;
using Maydel.Domain.StaffAttendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.StaffAttendance;

public sealed record ClockSettingsDto(TimeOnly StartsAt, TimeOnly EndsAt, int GraceMinutes, bool RequireLocation, double? Latitude, double? Longitude,
    int RadiusMeters, bool KioskLinkIssued);

public sealed record ClockSettingsInput(TimeOnly StartsAt, TimeOnly EndsAt, int GraceMinutes, bool RequireLocation, double? Latitude, double? Longitude, int RadiusMeters);

/// <summary>The kiosk link's secret token, shown once (open <c>/clock-kiosk?token=…</c> on the reception tablet).</summary>
public sealed record KioskLink(string Token);

/// <summary>What the kiosk shows: the current code, the link to encode in the QR, and when it changes.</summary>
public sealed record KioskCode(string Code, string Url, DateTimeOffset ExpiresAt, string School);

public sealed record ClockInput(string Code, double? Latitude, double? Longitude);

public enum DayState
{
    In,
    Late,
    Out,
    Absent,
}

public sealed record StaffDayDto(Guid StaffId, string Name, string? JobTitle, DateOnly Date, DateTimeOffset? ClockInAt, DateTimeOffset? ClockOutAt, bool IsLate,
    DayState State, double? HoursWorked, ClockMethod? InMethod, ClockMethod? OutMethod, string? Note);

public sealed record ClockResult(bool ClockedIn, StaffDayDto Day);

public sealed record DayBoard(DateOnly Date, bool SchoolDay, int Present, int Late, int Absent, IReadOnlyList<StaffDayDto> Staff);

public sealed record StaffSummary(Guid StaffId, string Name, string? JobTitle, int SchoolDays, int DaysPresent, int DaysLate, int DaysAbsent,
    TimeOnly? AverageArrival, double HoursWorked);

public sealed record AttendanceReport(DateOnly From, DateOnly To, int SchoolDays, IReadOnlyList<StaffSummary> Staff);

public sealed record CorrectionInput(DateTimeOffset? ClockInAt, DateTimeOffset? ClockOutAt, string Note);

public static class ClockErrors
{
    public static readonly Error NotStaff = Error.Forbidden("clock.not_staff", "Only staff with a staff record can clock in.");
    public static readonly Error BadCode = Error.Rule("clock.code", "That code has expired or isn't valid. Scan the code on the school screen again.");
    public static readonly Error Weekend = Error.Rule("clock.weekend", "Clocking in is for school days (Monday to Friday).");
    public static readonly Error KioskToken = Error.Unauthorized("clock.kiosk", "This kiosk link is no longer valid. Ask the admin for a new one.");
    public static readonly Error NoKioskLink = Error.NotFound("clock.no_kiosk", "There's no kiosk link to show. Issue one first.");
    public static Error TooFar(int metres) => Error.Rule("clock.too_far", $"You need to be at the school to clock in (you seem to be about {metres} m away).");
    public static readonly Error NeedLocation = Error.Validation("latitude", "Allow location access so we can confirm you're at the school.");
}

internal static class Clock
{
    public static async Task<ClockSettings> SettingsAsync(this IAppDbContext db, CancellationToken ct)
    {
        var s = await db.ClockSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            db.ClockSettings.Add(s = new ClockSettings());
            await db.SaveChangesAsync(ct);
        }
        return s;
    }

    public static bool IsSchoolDay(DateOnly d) => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    public static StaffDayDto Dto(Domain.People.Staff s, DateOnly date, StaffDay? d) =>
        new(s.Id, s.FullName, s.JobTitle, date, d?.ClockInAt, d?.ClockOutAt, d?.IsLate ?? false,
            d?.ClockInAt is null ? DayState.Absent : d.ClockOutAt is not null ? DayState.Out : d.IsLate ? DayState.Late : DayState.In,
            d?.Worked is { } w ? Math.Round(w.TotalHours, 2) : null, d?.ClockInAt is null ? null : d.InMethod, d?.OutMethod, d?.Note);
}

// ---------- Teacher ----------

/// <summary>A staff member scans the kiosk QR: first scan of the day clocks in, second clocks out. Server time decides.</summary>
public sealed record ClockCommand(ClockInput Input) : ICommand<ClockResult>;

public sealed record MyDaysQuery(DateOnly? From = null, DateOnly? To = null) : IQuery<IReadOnlyList<StaffDayDto>>;

internal sealed class ClockHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : ICommandHandler<ClockCommand, ClockResult>
{
    public async Task<Result<ClockResult>> Handle(ClockCommand c, CancellationToken ct)
    {
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.UserId == me.UserId && s.IsActive, ct);
        if (staff is null) return ClockErrors.NotStaff;
        var settings = await db.SettingsAsync(ct);
        var now = clock.Now.ToUniversalTime();
        if (!settings.Accepts(c.Input.Code, now)) return ClockErrors.BadCode;
        var today = RegisterRules.TodayAt(now);
        if (!Clock.IsSchoolDay(today)) return ClockErrors.Weekend;
        if (settings.RequireLocation)
        {
            if (c.Input.Latitude is not { } lat || c.Input.Longitude is not { } lon) return ClockErrors.NeedLocation;
            var metres = ClockSettings.Distance(lat, lon, settings.Latitude!.Value, settings.Longitude!.Value);
            if (metres > settings.RadiusMeters) return ClockErrors.TooFar((int)metres);
        }
        var day = await db.StaffDays.FirstOrDefaultAsync(d => d.StaffId == staff.Id && d.Date == today, ct);
        if (day is null) db.StaffDays.Add(day = new StaffDay { StaffId = staff.Id, Date = today });
        if (day.Scan(now, settings, c.Input.Latitude, c.Input.Longitude, out var clockedIn) is { } invalid) return invalid.ToError();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("clock.twice", "That scan was received twice. Check your status and scan again if needed.");
        }
        return new ClockResult(clockedIn, Clock.Dto(staff, today, day));
    }
}

internal sealed class MyDaysHandler(IAppDbContext db, ICurrentUser me, ISchoolClock clock) : IQueryHandler<MyDaysQuery, IReadOnlyList<StaffDayDto>>
{
    public async Task<Result<IReadOnlyList<StaffDayDto>>> Handle(MyDaysQuery q, CancellationToken ct)
    {
        var staff = await db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == me.UserId, ct);
        if (staff is null) return ClockErrors.NotStaff;
        var to = q.To ?? clock.Today;
        var from = q.From ?? to.AddDays(-30);
        var days = await db.StaffDays.AsNoTracking().Where(d => d.StaffId == staff.Id && d.Date >= from && d.Date <= to).OrderByDescending(d => d.Date).ToListAsync(ct);
        return days.Select(d => Clock.Dto(staff, d.Date, d)).ToList();
    }
}

// ---------- Kiosk ----------

/// <summary>The reception tablet asks for the current code with its kiosk token (no one signed in on it).</summary>
public sealed record KioskCodeQuery(string Token) : IQuery<KioskCode>;

internal sealed class KioskCodeHandler(IAppDbContext db, ISchoolClock clock, IOptions<EmailOptions> options) : IQueryHandler<KioskCodeQuery, KioskCode>
{
    public async Task<Result<KioskCode>> Handle(KioskCodeQuery q, CancellationToken ct)
    {
        var settings = await db.SettingsAsync(ct);
        if (settings.KioskTokenHash is null || string.IsNullOrWhiteSpace(q.Token)
            || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(ClockSettings.HashToken(q.Token)), System.Text.Encoding.UTF8.GetBytes(settings.KioskTokenHash)))
            return ClockErrors.KioskToken;
        var now = clock.Now.ToUniversalTime();
        var window = ClockSettings.WindowAt(now);
        var code = settings.CodeFor(window);
        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "School";
        var portal = (options.Value.PortalUrl ?? "").TrimEnd('/');
        return new KioskCode(code, $"{portal}/clock?c={Uri.EscapeDataString(code)}", DateTimeOffset.FromUnixTimeSeconds((window + 1) * ClockSettings.WindowSeconds), school);
    }
}

// ---------- Admin ----------

public sealed record GetClockSettingsQuery : IQuery<ClockSettingsDto>;
public sealed record SaveClockSettingsCommand(ClockSettingsInput Settings) : ICommand<ClockSettingsDto>;

/// <summary>Makes a new kiosk link (the old one stops working) and returns its token.</summary>
public sealed record IssueKioskLinkCommand : ICommand<KioskLink>;

/// <summary>The current kiosk link's token, to copy or open it again. Links issued before tokens were kept return not-found.</summary>
public sealed record GetKioskLinkQuery : IQuery<KioskLink>;

public sealed record DayBoardQuery(DateOnly? Date = null) : IQuery<DayBoard>;
public sealed record AttendanceReportQuery(DateOnly From, DateOnly To) : IQuery<AttendanceReport>;
public sealed record CorrectDayCommand(Guid StaffId, DateOnly Date, CorrectionInput Input) : ICommand<StaffDayDto>;

internal sealed class GetClockSettingsHandler(IAppDbContext db) : IQueryHandler<GetClockSettingsQuery, ClockSettingsDto>
{
    public async Task<Result<ClockSettingsDto>> Handle(GetClockSettingsQuery q, CancellationToken ct)
    {
        var s = await db.SettingsAsync(ct);
        return new ClockSettingsDto(s.StartsAt, s.EndsAt, s.GraceMinutes, s.RequireLocation, s.Latitude, s.Longitude, s.RadiusMeters, s.KioskTokenHash is not null);
    }
}

internal sealed class SaveClockSettingsHandler(IAppDbContext db) : ICommandHandler<SaveClockSettingsCommand, ClockSettingsDto>
{
    public async Task<Result<ClockSettingsDto>> Handle(SaveClockSettingsCommand c, CancellationToken ct)
    {
        var s = await db.SettingsAsync(ct);
        var i = c.Settings;
        (s.StartsAt, s.EndsAt, s.GraceMinutes, s.RequireLocation, s.Latitude, s.Longitude, s.RadiusMeters) =
            (i.StartsAt, i.EndsAt, i.GraceMinutes, i.RequireLocation, i.Latitude, i.Longitude, i.RadiusMeters);
        if (s.Check() is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return new ClockSettingsDto(s.StartsAt, s.EndsAt, s.GraceMinutes, s.RequireLocation, s.Latitude, s.Longitude, s.RadiusMeters, s.KioskTokenHash is not null);
    }
}

internal sealed class IssueKioskLinkHandler(IAppDbContext db) : ICommandHandler<IssueKioskLinkCommand, KioskLink>
{
    public async Task<Result<KioskLink>> Handle(IssueKioskLinkCommand c, CancellationToken ct)
    {
        var s = await db.SettingsAsync(ct);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        (s.KioskTokenHash, s.KioskToken) = (ClockSettings.HashToken(token), token);
        await db.SaveChangesAsync(ct);
        return new KioskLink(token);
    }
}

internal sealed class GetKioskLinkHandler(IAppDbContext db) : IQueryHandler<GetKioskLinkQuery, KioskLink>
{
    public async Task<Result<KioskLink>> Handle(GetKioskLinkQuery q, CancellationToken ct)
    {
        var s = await db.SettingsAsync(ct);
        return s.KioskToken is { } token && s.KioskTokenHash == ClockSettings.HashToken(token) ? new KioskLink(token) : ClockErrors.NoKioskLink;
    }
}

internal sealed class DayBoardHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<DayBoardQuery, DayBoard>
{
    public async Task<Result<DayBoard>> Handle(DayBoardQuery q, CancellationToken ct)
    {
        var date = q.Date ?? clock.Today;
        var staff = await db.Staff.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.FullName).ToListAsync(ct);
        var days = await db.StaffDays.AsNoTracking().Where(d => d.Date == date).ToDictionaryAsync(d => d.StaffId, ct);
        var rows = staff.Select(s => Clock.Dto(s, date, days.GetValueOrDefault(s.Id)))
            .OrderBy(r => r.State == DayState.Absent).ThenBy(r => r.ClockInAt).ThenBy(r => r.Name).ToList();
        return new DayBoard(date, Clock.IsSchoolDay(date), rows.Count(r => r.State != DayState.Absent), rows.Count(r => r.IsLate),
            Clock.IsSchoolDay(date) ? rows.Count(r => r.State == DayState.Absent) : 0, rows);
    }
}

internal sealed class AttendanceReportHandler(IAppDbContext db) : IQueryHandler<AttendanceReportQuery, AttendanceReport>
{
    public async Task<Result<AttendanceReport>> Handle(AttendanceReportQuery q, CancellationToken ct)
    {
        if (q.To < q.From || q.To.DayNumber - q.From.DayNumber > 370) return Error.Validation("to", "Choose a range of up to a year, ending after it starts.");
        var schoolDays = Enumerable.Range(0, q.To.DayNumber - q.From.DayNumber + 1).Select(i => q.From.AddDays(i)).Count(Clock.IsSchoolDay);
        var staff = await db.Staff.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.FullName).ToListAsync(ct);
        var days = await db.StaffDays.AsNoTracking().Where(d => d.Date >= q.From && d.Date <= q.To && d.ClockInAt != null).ToListAsync(ct);
        var rows = staff.Select(s =>
        {
            var mine = days.Where(d => d.StaffId == s.Id).ToList();
            TimeOnly? avg = mine.Count == 0 ? null
                : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Round(mine.Average(d => d.ClockInAt!.Value.ToOffset(RegisterRules.SchoolUtcOffset).TimeOfDay.TotalMinutes))));
            return new StaffSummary(s.Id, s.FullName, s.JobTitle, schoolDays, mine.Count, mine.Count(d => d.IsLate), Math.Max(0, schoolDays - mine.Count),
                avg, Math.Round(mine.Sum(d => d.Worked?.TotalHours ?? 0), 2));
        }).ToList();
        return new AttendanceReport(q.From, q.To, schoolDays, rows);
    }
}

internal sealed class CorrectDayHandler(IAppDbContext db) : ICommandHandler<CorrectDayCommand, StaffDayDto>
{
    public async Task<Result<StaffDayDto>> Handle(CorrectDayCommand c, CancellationToken ct)
    {
        var staff = await db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.Id == c.StaffId, ct);
        if (staff is null) return Error.NotFound("staff.not_found", "That staff member doesn't exist.");
        var settings = await db.SettingsAsync(ct);
        var day = await db.StaffDays.FirstOrDefaultAsync(d => d.StaffId == c.StaffId && d.Date == c.Date, ct);
        if (day is null) db.StaffDays.Add(day = new StaffDay { StaffId = c.StaffId, Date = c.Date });
        if (day.Correct(c.Input.ClockInAt?.ToUniversalTime(), c.Input.ClockOutAt?.ToUniversalTime(), c.Input.Note, settings) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return Clock.Dto(staff, c.Date, day);
    }
}
