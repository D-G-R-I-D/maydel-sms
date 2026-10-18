using Maydel.Api.Common;
using Maydel.Application.Cbt;
using Maydel.Application.Cbt.GetAttempt;
using Maydel.Application.Cbt.GetAttemptImage;
using Maydel.Application.Cbt.ListMyTests;
using Maydel.Application.Cbt.LogAttemptEvent;
using Maydel.Application.Cbt.SaveAnswer;
using Maydel.Application.Cbt.StartAttempt;
using Maydel.Application.Cbt.SubmitAttempt;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Students sitting online tests in the portal (E16.3, E16.6).</summary>
public static class StudentTestEndpoints
{
    public static void MapStudentTestEndpoints(this IEndpointRouteBuilder app)
    {
        var student = app.MapGroup("/api/student").WithTags("CBT (students)").RequireAuthorization(Policies.Students);

        student.MapGet("/tests", List).WithName("ListMyTests").ProducesProblems(401, 403);
        student.MapPost("/tests/{id:guid}/start", Start).WithName("StartTestAttempt").ProducesProblems(401, 403, 404, 409, 422);
        student.MapGet("/attempts/{id:guid}", Get).WithName("GetTestAttempt").ProducesProblems(401, 403, 404);
        student.MapPut("/attempts/{id:guid}/items/{itemId:guid}", Save).WithName("SaveTestAnswer").ProducesProblems(400, 401, 403, 404, 409, 422);
        student.MapPost("/attempts/{id:guid}/events", Event).WithName("LogTestAttemptEvent").ProducesProblems(400, 401, 403, 404, 409, 422);
        student.MapPost("/attempts/{id:guid}/submit", Submit).WithName("SubmitTestAttempt").ProducesProblems(401, 403, 404, 409, 422);
        student.MapGet("/attempts/{id:guid}/items/{itemId:guid}/image", Image).WithName("GetTestAttemptImage").ProducesProblems(401, 403, 404);
    }

    private static async Task<Results<Ok<IReadOnlyList<StudentTestDto>>, ProblemHttpResult>> List(IQueryHandler<ListMyTestsQuery, IReadOnlyList<StudentTestDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListMyTestsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<AttemptPaper>, ProblemHttpResult>> Start(Guid id, ICommandHandler<StartAttemptCommand, AttemptPaper> h, CancellationToken ct) =>
        (await h.Handle(new StartAttemptCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<AttemptPaper>, ProblemHttpResult>> Get(Guid id, IQueryHandler<GetAttemptQuery, AttemptPaper> h, CancellationToken ct) =>
        (await h.Handle(new GetAttemptQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<AnswerSaved>, ProblemHttpResult>> Save(Guid id, Guid itemId, AnswerInput body, ICommandHandler<SaveAnswerCommand, AnswerSaved> h, CancellationToken ct) =>
        (await h.Handle(new SaveAnswerCommand(id, itemId, body), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> Event(Guid id, EventInput body, ICommandHandler<LogAttemptEventCommand> h, CancellationToken ct) =>
        (await h.Handle(new LogAttemptEventCommand(id, body), ct)).ToHttpResult();

    private static async Task<Results<Ok<AttemptPaper>, ProblemHttpResult>> Submit(Guid id, TokenInput body, ICommandHandler<SubmitAttemptCommand, AttemptPaper> h, CancellationToken ct) =>
        (await h.Handle(new SubmitAttemptCommand(id, body.SessionToken), ct)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> Image(Guid id, Guid itemId, IQueryHandler<GetAttemptImageQuery, PhotoContent> h, CancellationToken ct) =>
        PhotoEndpoints.ToFile(await h.Handle(new GetAttemptImageQuery(id, itemId), ct));
}
