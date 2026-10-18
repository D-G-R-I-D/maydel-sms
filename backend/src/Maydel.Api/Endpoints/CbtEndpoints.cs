using Maydel.Api.Common;
using Maydel.Application.Cbt;
using Maydel.Application.Cbt.ArchiveQuestion;
using Maydel.Application.Cbt.GetQuestion;
using Maydel.Application.Cbt.GetQuestionImage;
using Maydel.Application.Cbt.ListAuthoringScopes;
using Maydel.Application.Cbt.ListQuestions;
using Maydel.Application.Cbt.ListTopics;
using Maydel.Application.Cbt.SaveQuestion;
using Maydel.Application.Cbt.SetQuestionImage;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Maydel.Api.Endpoints;

/// <summary>Online tests and exams (E16): the question bank first.</summary>
public static class CbtEndpoints
{
    public static void MapCbtEndpoints(this IEndpointRouteBuilder app)
    {
        var bank = app.MapGroup("/api/cbt").WithTags("CBT").RequireAuthorization(Policies.Academic);

        bank.MapGet("/scopes", Scopes).WithName("ListAuthoringScopes").ProducesProblems(401, 403);
        bank.MapGet("/topics", Topics).WithName("ListQuestionTopics").ProducesProblems(401, 403);
        bank.MapGet("/questions", List).WithName("ListQuestions").ProducesProblems(401, 403);
        bank.MapGet("/questions/{id:guid}", Get).WithName("GetQuestion").ProducesProblems(401, 403, 404);
        bank.MapPost("/questions", Create).WithName("CreateQuestion").ProducesProblems(400, 401, 403);
        bank.MapPut("/questions/{id:guid}", Update).WithName("UpdateQuestion").ProducesProblems(400, 401, 403, 404);
        bank.MapDelete("/questions/{id:guid}", Archive).WithName("ArchiveQuestion").ProducesProblems(401, 403, 404);
        bank.MapPut("/questions/{id:guid}/image", SetImage).WithName("SetQuestionImage")
            .DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(PhotoStore.MaxUploadBytes + 64 * 1024))
            .Accepts<IFormFile>("multipart/form-data").ProducesProblems(400, 401, 403, 404);
        bank.MapDelete("/questions/{id:guid}/image", RemoveImage).WithName("RemoveQuestionImage").ProducesProblems(401, 403, 404);
        bank.MapGet("/questions/{id:guid}/image", Image).WithName("GetQuestionImage").ProducesProblems(401, 403, 404);
    }

    private static async Task<Results<Ok<IReadOnlyList<AuthoringScope>>, ProblemHttpResult>> Scopes(IQueryHandler<ListAuthoringScopesQuery, IReadOnlyList<AuthoringScope>> h, CancellationToken ct) =>
        (await h.Handle(new ListAuthoringScopesQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<string>>, ProblemHttpResult>> Topics([AsParameters] ListTopicsQuery query, IQueryHandler<ListTopicsQuery, IReadOnlyList<string>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<QuestionDto>>, ProblemHttpResult>> List([AsParameters] ListQuestionsQuery query, IQueryHandler<ListQuestionsQuery, PagedResult<QuestionDto>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<QuestionDto>, ProblemHttpResult>> Get(Guid id, IQueryHandler<GetQuestionQuery, QuestionDto> h, CancellationToken ct) =>
        (await h.Handle(new GetQuestionQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<QuestionDto>, ProblemHttpResult>> Create(QuestionInput body, ICommandHandler<SaveQuestionCommand, QuestionDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveQuestionCommand(null, body), ct)).ToHttpResult();

    private static async Task<Results<Ok<QuestionDto>, ProblemHttpResult>> Update(Guid id, QuestionInput body, ICommandHandler<SaveQuestionCommand, QuestionDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveQuestionCommand(id, body), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> Archive(Guid id, ICommandHandler<ArchiveQuestionCommand> h, CancellationToken ct) =>
        (await h.Handle(new ArchiveQuestionCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<QuestionDto>, ProblemHttpResult>> SetImage(Guid id, IFormFile? file, ICommandHandler<SetQuestionImageCommand, QuestionDto> h, CancellationToken ct) =>
        await PhotoEndpoints.ReadAsync(file, ct) is { } bytes
            ? (await h.Handle(new SetQuestionImageCommand(id, bytes), ct)).ToHttpResult()
            : PhotoErrors.Invalid.ToProblem();

    private static async Task<Results<Ok<QuestionDto>, ProblemHttpResult>> RemoveImage(Guid id, ICommandHandler<SetQuestionImageCommand, QuestionDto> h, CancellationToken ct) =>
        (await h.Handle(new SetQuestionImageCommand(id, null), ct)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> Image(Guid id, IQueryHandler<GetQuestionImageQuery, PhotoContent> h, CancellationToken ct) =>
        PhotoEndpoints.ToFile(await h.Handle(new GetQuestionImageQuery(id), ct));
}
