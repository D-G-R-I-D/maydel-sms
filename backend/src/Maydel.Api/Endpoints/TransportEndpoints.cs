using System.Globalization;
using System.Text;
using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Transport;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// The school bus (E18): admins set up buses, runs and the checklist and see the day and reports; drivers tap Left/Back
/// for each run and fill in the daily checklist from their phone. Drivers see no student data.
/// </summary>
public static class TransportEndpoints
{
    public static void MapTransportEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/transport").WithTags("Transport").RequireAuthorization(Policies.Admin);
        admin.MapGet("/buses", Buses).WithName("ListBuses").ProducesProblems(401, 403);
        admin.MapPost("/buses", CreateBus).WithName("CreateBus").ProducesProblems(400, 401, 403);
        admin.MapPut("/buses/{id:guid}", UpdateBus).WithName("UpdateBus").ProducesProblems(400, 401, 403, 404);
        admin.MapGet("/runs", Runs).WithName("ListBusRuns").ProducesProblems(401, 403);
        admin.MapPut("/runs", SaveRuns).WithName("SaveBusRuns").ProducesProblems(400, 401, 403, 404);
        admin.MapGet("/questions", Questions).WithName("ListCheckQuestions").ProducesProblems(401, 403);
        admin.MapPut("/questions", SaveQuestions).WithName("SaveCheckQuestions").ProducesProblems(400, 401, 403, 404);
        admin.MapGet("/board", Board).WithName("GetBusDayBoard").ProducesProblems(401, 403);
        admin.MapGet("/trips", Trips).WithName("GetTripReport").ProducesProblems(400, 401, 403);
        admin.MapGet("/trips.csv", TripsCsv).WithName("ExportTripReport").Produces(StatusCodes.Status200OK, contentType: "text/csv").ProducesProblems(400, 401, 403);
        admin.MapPut("/trips/{busId:guid}/{runId:guid}/{date}", Correct).WithName("CorrectTrip").ProducesProblems(400, 401, 403, 404);
        admin.MapGet("/checks", Checks).WithName("ListBusChecks").ProducesProblems(401, 403);

        var driver = app.MapGroup("/api/driver").WithTags("Transport").RequireAuthorization(Policies.Transport);
        driver.MapGet("/today", Today).WithName("GetDriverToday").ProducesProblems(401, 403);
        driver.MapPost("/trips", Tap).WithName("TapTrip").ProducesProblems(401, 403, 404, 422);
        driver.MapPost("/trips/undo", Undo).WithName("UndoTripTap").ProducesProblems(401, 403, 404, 422);
        driver.MapPost("/checks", SubmitCheck).WithName("SubmitBusCheck").ProducesProblems(400, 401, 403, 404);
    }

    private static async Task<Results<Ok<IReadOnlyList<BusDto>>, ProblemHttpResult>> Buses(IQueryHandler<ListBusesQuery, IReadOnlyList<BusDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListBusesQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<BusDto>, ProblemHttpResult>> CreateBus(BusInput body, ICommandHandler<SaveBusCommand, BusDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveBusCommand(null, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<BusDto>, ProblemHttpResult>> UpdateBus(Guid id, BusInput body, ICommandHandler<SaveBusCommand, BusDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveBusCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<RunDto>>, ProblemHttpResult>> Runs(IQueryHandler<ListRunsQuery, IReadOnlyList<RunDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListRunsQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<RunDto>>, ProblemHttpResult>> SaveRuns(IReadOnlyList<RunInput> body, ICommandHandler<SaveRunsCommand, IReadOnlyList<RunDto>> h, CancellationToken ct) =>
        (await h.Handle(new SaveRunsCommand(body), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<CheckQuestionDto>>, ProblemHttpResult>> Questions(IQueryHandler<ListCheckQuestionsQuery, IReadOnlyList<CheckQuestionDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListCheckQuestionsQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<CheckQuestionDto>>, ProblemHttpResult>> SaveQuestions(IReadOnlyList<CheckQuestionInput> body, ICommandHandler<SaveCheckQuestionsCommand, IReadOnlyList<CheckQuestionDto>> h, CancellationToken ct) =>
        (await h.Handle(new SaveCheckQuestionsCommand(body), ct)).ToHttpResult();
    private static async Task<Results<Ok<BusDayBoard>, ProblemHttpResult>> Board(DateOnly? date, IQueryHandler<GetBusDayBoardQuery, BusDayBoard> h, CancellationToken ct) =>
        (await h.Handle(new GetBusDayBoardQuery(date), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<TripDto>>, ProblemHttpResult>> Trips(DateOnly from, DateOnly to, Guid? busId, IQueryHandler<TripReportQuery, IReadOnlyList<TripDto>> h, CancellationToken ct) =>
        (await h.Handle(new TripReportQuery(from, to, busId), ct)).ToHttpResult();

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> TripsCsv(DateOnly from, DateOnly to, Guid? busId, IQueryHandler<TripReportQuery, IReadOnlyList<TripDto>> h, CancellationToken ct)
    {
        var result = await h.Handle(new TripReportQuery(from, to, busId), ct);
        if (result.IsFailure) return result.Error!.ToProblem();
        var wat = TimeSpan.FromHours(1);
        string T(DateTimeOffset? t) => t?.ToOffset(wat).ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";
        var csv = new StringBuilder("Date,Bus,Run,Left,Back,Minutes,Note,Correction\r\n");
        foreach (var r in result.Value)
            csv.Append(string.Join(',', r.Date.ToString("yyyy-MM-dd"), Csv.Cell(r.Bus), Csv.Cell(r.Run), T(r.LeftAt), T(r.BackAt), r.Minutes?.ToString(CultureInfo.InvariantCulture) ?? "",
                Csv.Cell(r.Note), Csv.Cell(r.Correction))).Append("\r\n");
        return TypedResults.File(Csv.Bytes(csv), "text/csv", $"Bus trips {from:yyyy-MM-dd} to {to:yyyy-MM-dd}.csv");
    }

    private static async Task<Results<Ok<TripDto>, ProblemHttpResult>> Correct(Guid busId, Guid runId, DateOnly date, CorrectTripInput body, ICommandHandler<CorrectTripCommand, TripDto> h, CancellationToken ct) =>
        (await h.Handle(new CorrectTripCommand(busId, runId, date, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<CheckDto>>, ProblemHttpResult>> Checks([AsParameters] ListChecksQuery q, IQueryHandler<ListChecksQuery, IReadOnlyList<CheckDto>> h, CancellationToken ct) =>
        (await h.Handle(q, ct)).ToHttpResult();

    private static async Task<Results<Ok<DriverToday>, ProblemHttpResult>> Today(IQueryHandler<GetDriverTodayQuery, DriverToday> h, CancellationToken ct) =>
        (await h.Handle(new GetDriverTodayQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<TripDto>, ProblemHttpResult>> Tap(TapInput body, ICommandHandler<TapTripCommand, TripDto> h, CancellationToken ct) =>
        (await h.Handle(new TapTripCommand(body), ct)).ToHttpResult();
    private static async Task<Results<Ok<TripDto>, ProblemHttpResult>> Undo(UndoInput body, ICommandHandler<UndoTapCommand, TripDto> h, CancellationToken ct) =>
        (await h.Handle(new UndoTapCommand(body), ct)).ToHttpResult();
    private static async Task<Results<Ok<CheckDto>, ProblemHttpResult>> SubmitCheck(CheckInput body, ICommandHandler<SubmitCheckCommand, CheckDto> h, CancellationToken ct) =>
        (await h.Handle(new SubmitCheckCommand(body), ct)).ToHttpResult();
}
