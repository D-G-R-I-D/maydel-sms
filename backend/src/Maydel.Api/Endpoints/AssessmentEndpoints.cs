using Maydel.Api.Common;
using Maydel.Application.Assessment;
using Maydel.Application.Assessment.GetAssessmentSettings;
using Maydel.Application.Assessment.GetGradeSheet;
using Maydel.Application.Assessment.ListGradableClasses;
using Maydel.Application.Assessment.SaveAssessmentSettings;
using Maydel.Application.Assessment.SaveGradeSheet;
using Maydel.Application.Common.Messaging;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Assessment settings (E2.4) and score entry (E5.1–E5.2). Admins and teachers only; which class/subject a teacher may
/// grade is a row-level rule (AccessRules.CanGradeAsync).
/// </summary>
public static class AssessmentEndpoints
{
    public static void MapAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        var academic = app.MapGroup("/api/assessment").WithTags("Assessment").RequireAuthorization(Policies.Academic);

        academic.MapGet("/settings", GetSettings).WithName("GetAssessmentSettings").ProducesProblems(401, 403);
        academic.MapPut("/settings", SaveSettings).RequireAuthorization(Policies.Admin).WithName("SaveAssessmentSettings").ProducesProblems(400, 401, 403, 422);
        academic.MapGet("/my-classes", ListGradable).WithName("ListGradableClasses").ProducesProblems(401, 403);
        academic.MapGet("/gradesheet", GetGradeSheet).WithName("GetGradeSheet").ProducesProblems(400, 401, 403, 404);
        academic.MapPut("/gradesheet", SaveGradeSheet).WithName("SaveGradeSheet").ProducesProblems(400, 401, 403, 404, 422);
    }

    private static async Task<Results<Ok<AssessmentSettings>, ProblemHttpResult>> GetSettings(
        IQueryHandler<GetAssessmentSettingsQuery, AssessmentSettings> handler, CancellationToken ct) =>
        (await handler.Handle(new GetAssessmentSettingsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<AssessmentSettings>, ProblemHttpResult>> SaveSettings(
        SaveAssessmentSettingsCommand command, ICommandHandler<SaveAssessmentSettingsCommand, AssessmentSettings> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<GradableClass>>, ProblemHttpResult>> ListGradable(
        IQueryHandler<ListGradableClassesQuery, IReadOnlyList<GradableClass>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListGradableClassesQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<GradeSheet>, ProblemHttpResult>> GetGradeSheet(
        [AsParameters] GetGradeSheetQuery query, IQueryHandler<GetGradeSheetQuery, GradeSheet> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<GradeSheet>, ProblemHttpResult>> SaveGradeSheet(
        SaveGradeSheetCommand command, ICommandHandler<SaveGradeSheetCommand, GradeSheet> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();
}
