using System.Globalization;
using System.Text;
using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.StaffAttendance;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Staff clock-in/out with a rotating QR code at the school, and the admin's attendance board and reports.</summary>
public static class StaffAttendanceEndpoints
{
    public static void MapStaffAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/api/clock").WithTags("Staff attendance").RequireAuthorization(Policies.Staff);
        staff.MapPost("", ClockIn).WithName("Clock").ProducesProblems(400, 401, 403, 409, 422);
        staff.MapGet("/me", Mine).WithName("MyClockDays").ProducesProblems(401, 403);

        var kiosk = app.MapGroup("/api/public/clock").WithTags("Staff attendance").AllowAnonymous();
        kiosk.MapGet("/kiosk", Kiosk).WithName("GetKioskCode").RequireRateLimiting(RateLimits.PublicForms).ProducesProblems(401, 429);

        var admin = app.MapGroup("/api/clock").WithTags("Staff attendance").RequireAuthorization(Policies.Admin);
        admin.MapGet("/settings", Settings).WithName("GetClockSettings").ProducesProblems(401, 403);
        admin.MapPut("/settings", SaveSettings).WithName("SaveClockSettings").ProducesProblems(400, 401, 403);
        admin.MapGet("/kiosk-link", CurrentKioskLink).WithName("GetKioskLink").ProducesProblems(401, 403, 404);
        admin.MapPost("/kiosk-link", KioskLink).WithName("IssueKioskLink").ProducesProblems(401, 403);
        admin.MapGet("/board", Board).WithName("GetClockBoard").ProducesProblems(401, 403);
        admin.MapGet("/report", Report).WithName("GetClockReport").ProducesProblems(400, 401, 403);
        admin.MapGet("/report.csv", ReportCsv).WithName("ExportClockReport").Produces(StatusCodes.Status200OK, contentType: "text/csv").ProducesProblems(400, 401, 403);
        admin.MapPut("/days/{staffId:guid}/{date}", Correct).WithName("CorrectClockDay").ProducesProblems(400, 401, 403, 404);
    }

    private static async Task<Results<Ok<ClockResult>, ProblemHttpResult>> ClockIn(ClockInput body, ICommandHandler<ClockCommand, ClockResult> h, CancellationToken ct) =>
        (await h.Handle(new ClockCommand(body), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<StaffDayDto>>, ProblemHttpResult>> Mine(DateOnly? from, DateOnly? to, IQueryHandler<MyDaysQuery, IReadOnlyList<StaffDayDto>> h, CancellationToken ct) =>
        (await h.Handle(new MyDaysQuery(from, to), ct)).ToHttpResult();
    private static async Task<Results<Ok<KioskCode>, ProblemHttpResult>> Kiosk(string token, IQueryHandler<KioskCodeQuery, KioskCode> h, CancellationToken ct) =>
        (await h.Handle(new KioskCodeQuery(token), ct)).ToHttpResult();
    private static async Task<Results<Ok<ClockSettingsDto>, ProblemHttpResult>> Settings(IQueryHandler<GetClockSettingsQuery, ClockSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(new GetClockSettingsQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<ClockSettingsDto>, ProblemHttpResult>> SaveSettings(ClockSettingsInput body, ICommandHandler<SaveClockSettingsCommand, ClockSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveClockSettingsCommand(body), ct)).ToHttpResult();
    private static async Task<Results<Ok<KioskLink>, ProblemHttpResult>> CurrentKioskLink(IQueryHandler<GetKioskLinkQuery, KioskLink> h, CancellationToken ct) =>
        (await h.Handle(new GetKioskLinkQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<KioskLink>, ProblemHttpResult>> KioskLink(ICommandHandler<IssueKioskLinkCommand, KioskLink> h, CancellationToken ct) =>
        (await h.Handle(new IssueKioskLinkCommand(), ct)).ToHttpResult();
    private static async Task<Results<Ok<DayBoard>, ProblemHttpResult>> Board(DateOnly? date, IQueryHandler<DayBoardQuery, DayBoard> h, CancellationToken ct) =>
        (await h.Handle(new DayBoardQuery(date), ct)).ToHttpResult();
    private static async Task<Results<Ok<AttendanceReport>, ProblemHttpResult>> Report(DateOnly from, DateOnly to, IQueryHandler<AttendanceReportQuery, AttendanceReport> h, CancellationToken ct) =>
        (await h.Handle(new AttendanceReportQuery(from, to), ct)).ToHttpResult();
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ReportCsv(DateOnly from, DateOnly to, IQueryHandler<AttendanceReportQuery, AttendanceReport> h, CancellationToken ct)
    {
        var result = await h.Handle(new AttendanceReportQuery(from, to), ct);
        if (result.IsFailure) return result.Error!.ToProblem();
        var csv = new StringBuilder("Name,Job title,School days,Days present,Days late,Days absent,Average arrival,Hours worked\r\n");
        foreach (var r in result.Value.Staff)
            csv.Append(string.Join(',', Csv.Cell(r.Name), Csv.Cell(r.JobTitle), r.SchoolDays, r.DaysPresent, r.DaysLate, r.DaysAbsent,
                r.AverageArrival?.ToString("HH:mm") ?? "", r.HoursWorked.ToString("0.##", CultureInfo.InvariantCulture))).Append("\r\n");
        return TypedResults.File(Csv.Bytes(csv), "text/csv", $"Staff attendance {from:yyyy-MM-dd} to {to:yyyy-MM-dd}.csv");
    }
    private static async Task<Results<Ok<StaffDayDto>, ProblemHttpResult>> Correct(Guid staffId, DateOnly date, CorrectionInput body, ICommandHandler<CorrectDayCommand, StaffDayDto> h, CancellationToken ct) =>
        (await h.Handle(new CorrectDayCommand(staffId, date, body), ct)).ToHttpResult();
}
