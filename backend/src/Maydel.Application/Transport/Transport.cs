using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Notifications;
using Maydel.Domain.Attendance;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.Transport;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Transport;

public sealed record BusInput(string Name, string? PlateNumber, int Capacity, Guid? DriverStaffId, bool IsActive = true);
public sealed record BusDto(Guid Id, string Name, string? PlateNumber, int Capacity, Guid? DriverStaffId, string? DriverName, bool IsActive);
public sealed record RunInput(Guid? Id, string Name, RunKind Kind, int Order, bool IsActive = true);
public sealed record RunDto(Guid Id, string Name, RunKind Kind, int Order, bool IsActive);
public sealed record CheckQuestionInput(Guid? Id, string Text, CheckKind Kind, int Order, bool IsActive = true, bool ExpectedYes = true, bool FlagIfAnswered = false);
public sealed record CheckQuestionDto(Guid Id, string Text, CheckKind Kind, int Order, bool IsActive, bool ExpectedYes, bool FlagIfAnswered);

/// <summary>A run on a day for one bus. <c>Minutes</c>: how long the bus was out, once it's back.</summary>
public sealed record TripDto(Guid BusId, string Bus, Guid RunId, string Run, RunKind Kind, DateOnly Date, DateTimeOffset? LeftAt, DateTimeOffset? BackAt,
    int? Minutes, string? Note, string? Correction, bool CanUndo);

public sealed record CheckAnswerDto(Guid QuestionId, string Question, CheckKind Kind, bool? Yes, decimal? Number, string? Text, bool Flagged);
public sealed record CheckDto(Guid Id, Guid BusId, string Bus, DateOnly Date, string DriverName, DateTimeOffset SubmittedAt, bool IsFlagged, string? Notes,
    IReadOnlyList<CheckAnswerDto> Answers);

public sealed record BusDay(BusDto Bus, IReadOnlyList<TripDto> Trips, CheckDto? Check);

/// <summary>The admin's view of a day: every active bus, each active run, and the checklist.</summary>
public sealed record BusDayBoard(DateOnly Date, IReadOnlyList<BusDay> Buses);

/// <summary>What the driver needs on their phone today: their buses, today's runs, and the checklist.</summary>
public sealed record DriverToday(DateOnly Date, IReadOnlyList<BusDay> Buses, IReadOnlyList<CheckQuestionDto> Questions);

public enum TripEvent
{
    Left,
    Back,
}

public sealed record TapInput(Guid BusId, Guid RunId, TripEvent Event, string? Note);
public sealed record UndoInput(Guid BusId, Guid RunId);
public sealed record CheckAnswerInput(Guid QuestionId, bool? Yes, decimal? Number, string? Text);
public sealed record CheckInput(Guid BusId, IReadOnlyList<CheckAnswerInput> Answers, string? Notes);
public sealed record CorrectTripInput(DateTimeOffset? LeftAt, DateTimeOffset? BackAt, string Note);

public static class TransportErrors
{
    public static readonly Error BusNotFound = Error.NotFound("bus.not_found", "That bus doesn't exist.");
    public static readonly Error NotYourBus = Error.NotFound("bus.not_yours", "That isn't one of your buses.");
    public static readonly Error RunNotFound = Error.NotFound("run.not_found", "That run doesn't exist.");
    public static readonly Error NoDriver = Error.Forbidden("driver.no_staff", "Your account has no staff record. Ask the admin.");
}

internal static class TransportQueries
{
    public static async Task<IReadOnlyList<BusDto>> BusDtosAsync(this IAppDbContext db, IQueryable<Bus> buses, CancellationToken ct) =>
        await buses.OrderBy(b => b.Name).Select(b => new BusDto(b.Id, b.Name, b.PlateNumber, b.Capacity, b.DriverStaffId,
            db.Staff.Where(s => s.Id == b.DriverStaffId).Select(s => s.FullName).FirstOrDefault(), b.IsActive)).ToListAsync(ct);

    public static CheckDto Dto(this DailyCheck c, string bus) => new(c.Id, c.BusId, bus, c.Date, c.DriverName, c.SubmittedAt, c.IsFlagged, c.Notes,
        [.. c.Answers.Select(a => new CheckAnswerDto(a.QuestionId, a.Question, a.Kind, a.Yes, a.Number, a.Text, a.Flagged))]);

    public static TripDto Dto(BusDto bus, BusRun run, DateOnly date, TripLog? t, DateTimeOffset now) => new(bus.Id, bus.Name, run.Id, run.Name, run.Kind, date,
        t?.LeftAt, t?.BackAt, t is { LeftAt: { } l, BackAt: { } b } ? (int)Math.Round((b - l).TotalMinutes) : null, t?.Note, t?.Correction,
        t is not null && (t.BackAt ?? t.LeftAt) is { } last && now - last <= TripLog.UndoWindow);

    /// <summary>Each bus with each active run (logged or not) and the day's checklist.</summary>
    public static async Task<IReadOnlyList<BusDay>> DaysAsync(this IAppDbContext db, IReadOnlyList<BusDto> buses, DateOnly date, DateTimeOffset now, CancellationToken ct)
    {
        var ids = buses.Select(b => b.Id).ToList();
        var runs = await db.BusRuns.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Order).ToListAsync(ct);
        var trips = await db.TripLogs.AsNoTracking().Where(t => t.Date == date && ids.Contains(t.BusId)).ToListAsync(ct);
        var checks = await db.DailyChecks.AsNoTracking().Where(c => c.Date == date && ids.Contains(c.BusId)).ToListAsync(ct);
        return [.. buses.Select(b => new BusDay(b,
            [.. runs.Select(r => Dto(b, r, date, trips.FirstOrDefault(t => t.BusId == b.Id && t.RunId == r.Id), now))],
            checks.FirstOrDefault(c => c.BusId == b.Id)?.Dto(b.Name)))];
    }
}

// ---------- Admin: setup ----------

public sealed record ListBusesQuery : IQuery<IReadOnlyList<BusDto>>;
public sealed record SaveBusCommand(Guid? Id, BusInput Input) : ICommand<BusDto>;
public sealed record ListRunsQuery : IQuery<IReadOnlyList<RunDto>>;
public sealed record SaveRunsCommand(IReadOnlyList<RunInput> Runs) : ICommand<IReadOnlyList<RunDto>>;
public sealed record ListCheckQuestionsQuery : IQuery<IReadOnlyList<CheckQuestionDto>>;
public sealed record SaveCheckQuestionsCommand(IReadOnlyList<CheckQuestionInput> Questions) : ICommand<IReadOnlyList<CheckQuestionDto>>;

internal sealed class ListBusesHandler(IAppDbContext db) : IQueryHandler<ListBusesQuery, IReadOnlyList<BusDto>>
{
    public async Task<Result<IReadOnlyList<BusDto>>> Handle(ListBusesQuery q, CancellationToken ct) =>
        Result<IReadOnlyList<BusDto>>.Success(await db.BusDtosAsync(db.Buses.AsNoTracking(), ct));
}

internal sealed class SaveBusHandler(IAppDbContext db) : ICommandHandler<SaveBusCommand, BusDto>
{
    public async Task<Result<BusDto>> Handle(SaveBusCommand c, CancellationToken ct)
    {
        Bus bus;
        if (c.Id is { } id)
        {
            if (await db.Buses.FirstOrDefaultAsync(b => b.Id == id, ct) is not { } found) return TransportErrors.BusNotFound;
            bus = found;
        }
        else db.Buses.Add(bus = new Bus { Name = "" });
        var i = c.Input;
        if (i.DriverStaffId is { } staff && !await db.Staff.AnyAsync(s => s.Id == staff && s.IsActive, ct)) return Error.Validation("driverStaffId", "Choose an active staff member.");
        (bus.Name, bus.PlateNumber, bus.Capacity, bus.DriverStaffId, bus.IsActive) =
            (i.Name ?? "", string.IsNullOrWhiteSpace(i.PlateNumber) ? null : i.PlateNumber.Trim().ToUpperInvariant(), i.Capacity, i.DriverStaffId, i.IsActive);
        if (bus.Check() is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await db.BusDtosAsync(db.Buses.AsNoTracking().Where(b => b.Id == bus.Id), ct))[0];
    }
}

internal sealed class ListRunsHandler(IAppDbContext db) : IQueryHandler<ListRunsQuery, IReadOnlyList<RunDto>>
{
    public async Task<Result<IReadOnlyList<RunDto>>> Handle(ListRunsQuery q, CancellationToken ct) =>
        await db.BusRuns.AsNoTracking().OrderBy(r => r.Order).Select(r => new RunDto(r.Id, r.Name, r.Kind, r.Order, r.IsActive)).ToListAsync(ct);
}

/// <summary>Saves the whole list: existing runs by id, new ones without. Runs are never deleted (their history stays); retire them.</summary>
internal sealed class SaveRunsHandler(IAppDbContext db) : ICommandHandler<SaveRunsCommand, IReadOnlyList<RunDto>>
{
    public async Task<Result<IReadOnlyList<RunDto>>> Handle(SaveRunsCommand c, CancellationToken ct)
    {
        var existing = await db.BusRuns.ToDictionaryAsync(r => r.Id, ct);
        foreach (var i in c.Runs ?? [])
        {
            var name = (i.Name ?? "").Trim();
            if (name.Length is 0 or > 80) return Error.Validation("name", "Every run needs a name (up to 80 characters).");
            if (!Enum.IsDefined(i.Kind)) return Error.Validation("kind", "Choose Pickup or DropOff.");
            BusRun run;
            if (i.Id is { } id)
            {
                if (!existing.TryGetValue(id, out var found)) return TransportErrors.RunNotFound;
                run = found;
            }
            else db.BusRuns.Add(run = new BusRun { Name = name });
            (run.Name, run.Kind, run.Order, run.IsActive) = (name, i.Kind, i.Order, i.IsActive);
        }
        await db.SaveChangesAsync(ct);
        return await db.BusRuns.AsNoTracking().OrderBy(r => r.Order).Select(r => new RunDto(r.Id, r.Name, r.Kind, r.Order, r.IsActive)).ToListAsync(ct);
    }
}

internal sealed class ListCheckQuestionsHandler(IAppDbContext db) : IQueryHandler<ListCheckQuestionsQuery, IReadOnlyList<CheckQuestionDto>>
{
    public async Task<Result<IReadOnlyList<CheckQuestionDto>>> Handle(ListCheckQuestionsQuery q, CancellationToken ct) =>
        await db.CheckQuestions.AsNoTracking().OrderBy(x => x.Order)
            .Select(x => new CheckQuestionDto(x.Id, x.Text, x.Kind, x.Order, x.IsActive, x.ExpectedYes, x.FlagIfAnswered)).ToListAsync(ct);
}

/// <summary>Saves the whole checklist: existing questions by id, new ones without; retire rather than delete (past answers keep their wording).</summary>
internal sealed class SaveCheckQuestionsHandler(IAppDbContext db) : ICommandHandler<SaveCheckQuestionsCommand, IReadOnlyList<CheckQuestionDto>>
{
    public async Task<Result<IReadOnlyList<CheckQuestionDto>>> Handle(SaveCheckQuestionsCommand c, CancellationToken ct)
    {
        var existing = await db.CheckQuestions.ToDictionaryAsync(x => x.Id, ct);
        foreach (var i in c.Questions ?? [])
        {
            var text = (i.Text ?? "").Trim();
            if (text.Length is 0 or > 200) return Error.Validation("text", "Every question needs wording (up to 200 characters).");
            if (!Enum.IsDefined(i.Kind)) return Error.Validation("kind", "Choose YesNo, Number or Text.");
            CheckQuestion question;
            if (i.Id is { } id)
            {
                if (!existing.TryGetValue(id, out var found)) return Error.NotFound("question.not_found", "That question doesn't exist.");
                question = found;
            }
            else db.CheckQuestions.Add(question = new CheckQuestion { Text = text });
            (question.Text, question.Kind, question.Order, question.IsActive, question.ExpectedYes, question.FlagIfAnswered) =
                (text, i.Kind, i.Order, i.IsActive, i.ExpectedYes, i.FlagIfAnswered);
        }
        await db.SaveChangesAsync(ct);
        return await db.CheckQuestions.AsNoTracking().OrderBy(x => x.Order)
            .Select(x => new CheckQuestionDto(x.Id, x.Text, x.Kind, x.Order, x.IsActive, x.ExpectedYes, x.FlagIfAnswered)).ToListAsync(ct);
    }
}

// ---------- Admin: days, reports, corrections ----------

public sealed record GetBusDayBoardQuery(DateOnly? Date = null) : IQuery<BusDayBoard>;
public sealed record TripReportQuery(DateOnly From, DateOnly To, Guid? BusId = null) : IQuery<IReadOnlyList<TripDto>>;
public sealed record CorrectTripCommand(Guid BusId, Guid RunId, DateOnly Date, CorrectTripInput Input) : ICommand<TripDto>;
public sealed record ListChecksQuery(DateOnly? From = null, DateOnly? To = null, bool FlaggedOnly = false, Guid? BusId = null) : IQuery<IReadOnlyList<CheckDto>>;

internal sealed class GetBusDayBoardHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<GetBusDayBoardQuery, BusDayBoard>
{
    public async Task<Result<BusDayBoard>> Handle(GetBusDayBoardQuery q, CancellationToken ct)
    {
        var date = q.Date ?? clock.Today;
        var buses = await db.BusDtosAsync(db.Buses.AsNoTracking().Where(b => b.IsActive), ct);
        return new BusDayBoard(date, await db.DaysAsync(buses, date, clock.Now.ToUniversalTime(), ct));
    }
}

internal sealed class TripReportHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<TripReportQuery, IReadOnlyList<TripDto>>
{
    public async Task<Result<IReadOnlyList<TripDto>>> Handle(TripReportQuery q, CancellationToken ct)
    {
        if (q.To < q.From || q.To.DayNumber - q.From.DayNumber > 370) return Error.Validation("to", "Choose a range of up to a year, ending after it starts.");
        var trips = db.TripLogs.AsNoTracking().Where(t => t.Date >= q.From && t.Date <= q.To);
        if (q.BusId is { } bus) trips = trips.Where(t => t.BusId == bus);
        var rows = await trips.OrderBy(t => t.Date).ToListAsync(ct);
        var buses = (await db.BusDtosAsync(db.Buses.AsNoTracking(), ct)).ToDictionary(b => b.Id);
        var runs = await db.BusRuns.AsNoTracking().ToDictionaryAsync(r => r.Id, ct);
        var now = clock.Now.ToUniversalTime();
        return rows.Where(t => buses.ContainsKey(t.BusId) && runs.ContainsKey(t.RunId))
            .OrderBy(t => t.Date).ThenBy(t => buses[t.BusId].Name).ThenBy(t => runs[t.RunId].Order)
            .Select(t => TransportQueries.Dto(buses[t.BusId], runs[t.RunId], t.Date, t, now)).ToList();
    }
}

internal sealed class CorrectTripHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<CorrectTripCommand, TripDto>
{
    public async Task<Result<TripDto>> Handle(CorrectTripCommand c, CancellationToken ct)
    {
        var bus = (await db.BusDtosAsync(db.Buses.AsNoTracking().Where(b => b.Id == c.BusId), ct)).FirstOrDefault();
        if (bus is null) return TransportErrors.BusNotFound;
        if (await db.BusRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == c.RunId, ct) is not { } run) return TransportErrors.RunNotFound;
        var trip = await db.TripLogs.FirstOrDefaultAsync(t => t.BusId == c.BusId && t.RunId == c.RunId && t.Date == c.Date, ct);
        if (trip is null) db.TripLogs.Add(trip = new TripLog { BusId = c.BusId, RunId = c.RunId, Date = c.Date });
        if (trip.Correct(c.Input.LeftAt?.ToUniversalTime(), c.Input.BackAt?.ToUniversalTime(), c.Input.Note) is { } invalid) return invalid.ToError();
        foreach (var (at, field) in new[] { (trip.LeftAt, "leftAt"), (trip.BackAt, "backAt") })
            if (at is { } t && RegisterRules.TodayAt(t) != c.Date) return Error.Validation(field, "The time must be on the same day.");
        await db.SaveChangesAsync(ct);
        return TransportQueries.Dto(bus, run, c.Date, trip, clock.Now.ToUniversalTime());
    }
}

internal sealed class ListChecksHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<ListChecksQuery, IReadOnlyList<CheckDto>>
{
    public async Task<Result<IReadOnlyList<CheckDto>>> Handle(ListChecksQuery q, CancellationToken ct)
    {
        var to = q.To ?? clock.Today;
        var from = q.From ?? to.AddDays(-30);
        var checks = db.DailyChecks.AsNoTracking().Where(c => c.Date >= from && c.Date <= to);
        if (q.FlaggedOnly) checks = checks.Where(c => c.IsFlagged);
        if (q.BusId is { } bus) checks = checks.Where(c => c.BusId == bus);
        var rows = await checks.OrderByDescending(c => c.Date).Take(500).ToListAsync(ct);
        var names = await db.Buses.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        return rows.Select(c => c.Dto(names.GetValueOrDefault(c.BusId) ?? "")).ToList();
    }
}

// ---------- Driver ----------

public sealed record GetDriverTodayQuery : IQuery<DriverToday>;
public sealed record TapTripCommand(TapInput Input) : ICommand<TripDto>;
public sealed record UndoTapCommand(UndoInput Input) : ICommand<TripDto>;
public sealed record SubmitCheckCommand(CheckInput Input) : ICommand<CheckDto>;

/// <summary>The buses the signed-in user drives (admins: every active bus, to log for an absent driver).</summary>
internal sealed class DriverBuses(IAppDbContext db, ICurrentUser me)
{
    public async Task<IQueryable<Bus>?> MineAsync(CancellationToken ct)
    {
        var active = db.Buses.AsNoTracking().Where(b => b.IsActive);
        if (me.IsInRole(Roles.Admin)) return active;
        var staffId = await db.Staff.AsNoTracking().Where(s => s.UserId == me.UserId && s.IsActive).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        return staffId is { } id ? active.Where(b => b.DriverStaffId == id) : null;
    }

    public async Task<string> NameAsync(CancellationToken ct) =>
        await db.Staff.AsNoTracking().Where(s => s.UserId == me.UserId).Select(s => s.FullName).FirstOrDefaultAsync(ct) ?? me.UserName ?? "Driver";
}

internal sealed class GetDriverTodayHandler(IAppDbContext db, DriverBuses mine, ISchoolClock clock) : IQueryHandler<GetDriverTodayQuery, DriverToday>
{
    public async Task<Result<DriverToday>> Handle(GetDriverTodayQuery q, CancellationToken ct)
    {
        if (await mine.MineAsync(ct) is not { } buses) return TransportErrors.NoDriver;
        var today = clock.Today;
        var questions = await db.CheckQuestions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Order)
            .Select(x => new CheckQuestionDto(x.Id, x.Text, x.Kind, x.Order, x.IsActive, x.ExpectedYes, x.FlagIfAnswered)).ToListAsync(ct);
        return new DriverToday(today, await db.DaysAsync(await db.BusDtosAsync(buses, ct), today, clock.Now.ToUniversalTime(), ct), questions);
    }
}

internal sealed class TapTripHandler(IAppDbContext db, DriverBuses mine, ISchoolClock clock) : ICommandHandler<TapTripCommand, TripDto>
{
    public async Task<Result<TripDto>> Handle(TapTripCommand c, CancellationToken ct)
    {
        var i = c.Input;
        if (await mine.MineAsync(ct) is not { } buses) return TransportErrors.NoDriver;
        if ((await db.BusDtosAsync(buses.Where(b => b.Id == i.BusId), ct)).FirstOrDefault() is not { } bus) return TransportErrors.NotYourBus;
        if (await db.BusRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == i.RunId && r.IsActive, ct) is not { } run) return TransportErrors.RunNotFound;
        var now = clock.Now.ToUniversalTime();
        var today = clock.Today;
        var trip = await db.TripLogs.FirstOrDefaultAsync(t => t.BusId == i.BusId && t.RunId == i.RunId && t.Date == today, ct);
        if (trip is null) db.TripLogs.Add(trip = new TripLog { BusId = i.BusId, RunId = i.RunId, Date = today });
        if ((i.Event == TripEvent.Left ? trip.Leave(now) : trip.Return(now)) is { } invalid) return invalid.ToError();
        if (!string.IsNullOrWhiteSpace(i.Note)) trip.Note = i.Note.Trim() is { Length: > 500 } n ? n[..500] : i.Note.Trim();
        await db.SaveChangesAsync(ct);
        return TransportQueries.Dto(bus, run, today, trip, now);
    }
}

internal sealed class UndoTapHandler(IAppDbContext db, DriverBuses mine, ISchoolClock clock) : ICommandHandler<UndoTapCommand, TripDto>
{
    public async Task<Result<TripDto>> Handle(UndoTapCommand c, CancellationToken ct)
    {
        var i = c.Input;
        if (await mine.MineAsync(ct) is not { } buses) return TransportErrors.NoDriver;
        if ((await db.BusDtosAsync(buses.Where(b => b.Id == i.BusId), ct)).FirstOrDefault() is not { } bus) return TransportErrors.NotYourBus;
        if (await db.BusRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == i.RunId, ct) is not { } run) return TransportErrors.RunNotFound;
        var now = clock.Now.ToUniversalTime();
        var today = clock.Today;
        if (await db.TripLogs.FirstOrDefaultAsync(t => t.BusId == i.BusId && t.RunId == i.RunId && t.Date == today, ct) is not { } trip)
            return Error.Rule("trip.nothing", "There's nothing to undo.");
        if (trip.Undo(now) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return TransportQueries.Dto(bus, run, today, trip, now);
    }
}

internal sealed class SubmitCheckHandler(IAppDbContext db, DriverBuses mine, ICurrentUser me, ISchoolClock clock, Notifier notifier)
    : ICommandHandler<SubmitCheckCommand, CheckDto>
{
    public async Task<Result<CheckDto>> Handle(SubmitCheckCommand c, CancellationToken ct)
    {
        var i = c.Input;
        if (await mine.MineAsync(ct) is not { } buses) return TransportErrors.NoDriver;
        if (await buses.FirstOrDefaultAsync(b => b.Id == i.BusId, ct) is not { } bus) return TransportErrors.NotYourBus;
        var questions = await db.CheckQuestions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Order).ToListAsync(ct);
        var given = (i.Answers ?? []).GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.Last());
        var answers = new List<CheckAnswer>();
        foreach (var q in questions)
        {
            given.TryGetValue(q.Id, out var a);
            var answer = new CheckAnswer { QuestionId = q.Id, Question = q.Text, Kind = q.Kind };
            switch (q.Kind)
            {
                case CheckKind.YesNo:
                    if (a?.Yes is not { } yes) return Error.Validation("answers", $"Answer yes or no: {q.Text}");
                    (answer.Yes, answer.Flagged) = (yes, yes != q.ExpectedYes);
                    break;
                case CheckKind.Number:
                    if (a?.Number is not { } number || number < 0) return Error.Validation("answers", $"Enter a number: {q.Text}");
                    answer.Number = number;
                    break;
                default:
                    var text = a?.Text?.Trim() is { Length: > 0 } t ? t : null;
                    if (text is { Length: > 1000 }) return Error.Validation("answers", "Keep each answer under 1,000 characters.");
                    (answer.Text, answer.Flagged) = (text, q.FlagIfAnswered && text is not null);
                    break;
            }
            answers.Add(answer);
        }
        var notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
        if (notes is { Length: > 2000 }) return Error.Validation("notes", "Keep notes under 2,000 characters.");

        var today = clock.Today;
        var check = await db.DailyChecks.FirstOrDefaultAsync(x => x.BusId == bus.Id && x.Date == today, ct);
        var wasFlagged = check?.IsFlagged ?? false;
        if (check is null) db.DailyChecks.Add(check = new DailyCheck { BusId = bus.Id, Date = today, DriverName = "" });
        (check.DriverUserId, check.DriverName, check.SubmittedAt, check.Answers, check.Notes) =
            (me.UserId!.Value, await mine.NameAsync(ct), clock.Now.ToUniversalTime(), answers, notes);
        check.IsFlagged = answers.Any(a => a.Flagged);

        if (check.IsFlagged && !wasFlagged)
        {
            var problems = string.Join("; ", answers.Where(a => a.Flagged).Select(a => a.Text is { } t ? $"{a.Question} {t}" : a.Question));
            var admins = await db.UserRoles.Join(db.Roles.Where(r => r.Name == Roles.Admin), ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId).Distinct().ToListAsync(ct);
            foreach (var admin in admins)
                notifier.User(admin, null, NotificationKind.BusCheckFlagged, $"{bus.Name}: check needs attention", problems.Length > 300 ? problems[..300] + "…" : problems,
                    "/admin/transport", $"buscheck:{bus.Id}:{today:yyyy-MM-dd}");
        }
        await db.SaveChangesAsync(ct);
        return check.Dto(bus.Name);
    }
}
