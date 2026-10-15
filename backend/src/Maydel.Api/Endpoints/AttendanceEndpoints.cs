using Maydel.Api.Common;
using Maydel.Application.Attendance;
using Maydel.Application.Attendance.GetAttendanceSummary;
using Maydel.Application.Attendance.GetRegister;
using Maydel.Application.Attendance.SaveRegister;
using Maydel.Application.Common.Messaging;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Daily register (E9.1) and term summaries (E9.2). Viewing: admins and the class's teachers. Taking the register: the
/// form teacher (today) or an admin (any earlier school day). Totals feed the report card when attendance isn't entered by hand.
/// </summary>
public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var academic = app.MapGroup("/api/attendance").WithTags("Attendance").RequireAuthorization(Policies.Academic);

        academic.MapGet("/register", GetRegister).WithName("GetRegister").ProducesProblems(400, 401, 403, 404);
        academic.MapPut("/register", SaveRegister).WithName("SaveRegister").ProducesProblems(400, 401, 403, 404, 422);
        academic.MapGet("/summary", GetSummary).WithName("GetAttendanceSummary").ProducesProblems(400, 401, 403, 404);
    }

    private static async Task<Results<Ok<ClassRegister>, ProblemHttpResult>> GetRegister(
        [AsParameters] GetRegisterQuery query, IQueryHandler<GetRegisterQuery, ClassRegister> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<ClassRegister>, ProblemHttpResult>> SaveRegister(
        SaveRegisterCommand command, ICommandHandler<SaveRegisterCommand, ClassRegister> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<ClassAttendanceSummary>, ProblemHttpResult>> GetSummary(
        [AsParameters] GetAttendanceSummaryQuery query, IQueryHandler<GetAttendanceSummaryQuery, ClassAttendanceSummary> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();
}
