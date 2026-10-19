using Maydel.Api.Common;
using Maydel.Application.Cbt;
using Maydel.Application.Cbt.ArchiveQuestion;
using Maydel.Application.Cbt.GenerateQuestions;
using Maydel.Application.Cbt.GetAiQuota;
using Maydel.Application.Cbt.GetAiUsage;
using Maydel.Application.Cbt.GetGeneration;
using Maydel.Application.Cbt.ListGenerations;
using Maydel.Application.Cbt.RegenerateQuestions;
using Maydel.Application.Cbt.SaveAiSettings;
using Maydel.Application.Cbt.SaveDrafts;
using Maydel.Domain.Cbt;
using Maydel.Application.Cbt.DeleteTest;
using Maydel.Application.Cbt.GetAttemptReview;
using Maydel.Application.Cbt.GetTestAnalytics;
using Maydel.Application.Cbt.ListQuestionResponses;
using Maydel.Application.Cbt.ListTestAttempts;
using Maydel.Application.Cbt.MarkAnswer;
using Maydel.Application.Cbt.PreviewGradebookPush;
using Maydel.Application.Cbt.PushToGradebook;
using Maydel.Application.Cbt.ReleaseResults;
using Maydel.Application.Cbt.GetTest;
using Maydel.Application.Cbt.ListTests;
using Maydel.Application.Cbt.PublishTest;
using Maydel.Application.Cbt.SaveTest;
using Maydel.Application.Cbt.SaveTestSections;
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

        // Tests and exams (E16.2, E16.2a)
        bank.MapGet("/tests", Tests).WithName("ListCbtTests").ProducesProblems(401, 403);
        bank.MapGet("/tests/{id:guid}", Test).WithName("GetCbtTest").ProducesProblems(401, 403, 404);
        bank.MapPost("/tests", CreateTest).WithName("CreateCbtTest").ProducesProblems(400, 401, 403);
        bank.MapPut("/tests/{id:guid}", UpdateTest).WithName("UpdateCbtTest").ProducesProblems(400, 401, 403, 404, 422);
        bank.MapPut("/tests/{id:guid}/sections", Sections).WithName("SaveCbtTestSections").ProducesProblems(400, 401, 403, 404, 422);
        bank.MapPost("/tests/{id:guid}/publish", Publish).WithName("PublishCbtTest").ProducesProblems(400, 401, 403, 404, 422);
        bank.MapPost("/tests/{id:guid}/unpublish", Unpublish).WithName("UnpublishCbtTest").ProducesProblems(401, 403, 404, 422);
        bank.MapDelete("/tests/{id:guid}", DeleteTest).WithName("DeleteCbtTest").ProducesProblems(401, 403, 404, 422);

        // Marking, release, gradebook and analytics (E16.4, E16.5, E16.7)
        bank.MapGet("/tests/{id:guid}/attempts", Attempts).WithName("ListCbtTestAttempts").ProducesProblems(401, 403, 404);
        bank.MapGet("/tests/{id:guid}/questions/{questionId:guid}/responses", Responses).WithName("ListCbtQuestionResponses").ProducesProblems(401, 403, 404);
        bank.MapGet("/attempts/{id:guid}", Review).WithName("GetCbtAttemptReview").ProducesProblems(401, 403, 404);
        bank.MapPut("/attempts/{id:guid}/items/{itemId:guid}/mark", Mark).WithName("MarkCbtAnswer").ProducesProblems(400, 401, 403, 404, 422);
        bank.MapPost("/tests/{id:guid}/release", Release).WithName("ReleaseCbtResults").ProducesProblems(401, 403, 404, 422);
        bank.MapGet("/tests/{id:guid}/gradebook", GradebookPreview).WithName("PreviewCbtGradebookPush").ProducesProblems(401, 403, 404, 422);
        bank.MapPost("/tests/{id:guid}/gradebook", PushGradebook).WithName("PushCbtToGradebook").ProducesProblems(401, 403, 404, 422);
        bank.MapGet("/tests/{id:guid}/analytics", Analytics).WithName("GetCbtTestAnalytics").ProducesProblems(401, 403, 404);

        // AI question writing and quotas (E16.8, E16.9)
        bank.MapGet("/ai/quota", Quota).WithName("GetAiQuota").ProducesProblems(401, 403);
        bank.MapGet("/ai/generations", Generations).WithName("ListAiGenerations").ProducesProblems(401, 403);
        bank.MapGet("/ai/generations/{id:guid}", Generation).WithName("GetAiGeneration").ProducesProblems(401, 403, 404);
        bank.MapPost("/ai/generations", Generate).WithName("GenerateQuestions")
            .DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(5 * PhotoStore.MaxUploadBytes + 256 * 1024))
            .Accepts<IFormFileCollection>("multipart/form-data").ProducesProblems(400, 401, 403, 422);
        bank.MapPost("/ai/generations/{id:guid}/regenerate", Regenerate).WithName("RegenerateQuestions").ProducesProblems(401, 403, 404, 422);
        bank.MapPost("/ai/generations/{id:guid}/save", SaveDrafts).WithName("SaveAiQuestions").ProducesProblems(400, 401, 403, 404);
        var admin = app.MapGroup("/api/cbt/ai").WithTags("CBT").RequireAuthorization(Policies.Admin);
        admin.MapGet("/settings", AiSettingsGet).WithName("GetAiSettings").ProducesProblems(401, 403);
        admin.MapPut("/settings", AiSettingsSave).WithName("SaveAiSettings").ProducesProblems(400, 401, 403);
        admin.MapPut("/allowances/{userId:guid}", Allowance).WithName("SetAiAllowance").ProducesProblems(400, 401, 403, 404);
        admin.MapGet("/usage", Usage).WithName("GetAiUsage").ProducesProblems(401, 403);
    }

    private static async Task<Results<Ok<AiQuotaDto>, ProblemHttpResult>> Quota(IQueryHandler<GetAiQuotaQuery, AiQuotaDto> h, CancellationToken ct) =>
        (await h.Handle(new GetAiQuotaQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<AiGenerationSummary>>, ProblemHttpResult>> Generations(IQueryHandler<ListGenerationsQuery, IReadOnlyList<AiGenerationSummary>> h, CancellationToken ct) =>
        (await h.Handle(new ListGenerationsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<AiGenerationDto>, ProblemHttpResult>> Generation(Guid id, IQueryHandler<GetGenerationQuery, AiGenerationDto> h, CancellationToken ct) =>
        (await h.Handle(new GetGenerationQuery(id), ct)).ToHttpResult();

    /// <summary>Multipart: subjectId, classLevelId, termId?, assessmentType, kinds (repeat), count, easy, medium, hard, guidance?, files (repeat).</summary>
    private static async Task<Results<Ok<AiGenerationDto>, ProblemHttpResult>> Generate(HttpRequest request, ICommandHandler<GenerateQuestionsCommand, AiGenerationDto> h, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        static Guid? G(string? v) => Guid.TryParse(v, out var g) ? g : null;
        static int I(string? v) => int.TryParse(v, out var i) ? i : -1;
        if (G(form["subjectId"]) is not { } subject || G(form["classLevelId"]) is not { } level)
            return Error.Validation("subjectId", "Choose a subject and class level.").ToProblem();
        var kinds = new List<QuestionKind>();
        foreach (var k in form["kinds"].SelectMany(v => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (!Enum.TryParse<QuestionKind>(k, ignoreCase: true, out var kind)) return Error.Validation("kinds", $"\"{k}\" isn't a kind of question.").ToProblem();
            kinds.Add(kind);
        }
        if (!Enum.TryParse<AssessmentType>(form["assessmentType"], ignoreCase: true, out var type)) return Error.Validation("assessmentType", "Choose test, CAT or exam.").ToProblem();
        if (form.Files.Count > AiGeneration.MaxFiles) return Error.Validation("files", $"Upload at most {AiGeneration.MaxFiles} files.").ToProblem();
        var files = new List<UploadedFile>();
        foreach (var f in form.Files)
        {
            if (await PhotoEndpoints.ReadAsync(f, ct) is not { } bytes) return Error.Validation("files", $"\"{f.FileName}\" is empty or over 12 MB.").ToProblem();
            files.Add(new UploadedFile(f.FileName, bytes));
        }
        return (await h.Handle(new GenerateQuestionsCommand(subject, level, G(form["termId"]), type, kinds, I(form["count"]), I(form["easy"]), I(form["medium"]),
            I(form["hard"]), form["guidance"], files), ct)).ToHttpResult();
    }

    private static async Task<Results<Ok<AiGenerationDto>, ProblemHttpResult>> Regenerate(Guid id, ICommandHandler<RegenerateQuestionsCommand, AiGenerationDto> h, CancellationToken ct) =>
        (await h.Handle(new RegenerateQuestionsCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<QuestionDto>>, ProblemHttpResult>> SaveDrafts(Guid id, SaveDraftsInput body, ICommandHandler<SaveDraftsCommand, IReadOnlyList<QuestionDto>> h, CancellationToken ct) =>
        (await h.Handle(new SaveDraftsCommand(id, body), ct)).ToHttpResult();

    private static async Task<Results<Ok<AiSettingsDto>, ProblemHttpResult>> AiSettingsGet(IQueryHandler<GetAiSettingsQuery, AiSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(new GetAiSettingsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<AiSettingsDto>, ProblemHttpResult>> AiSettingsSave(SaveAiSettingsCommand command, ICommandHandler<SaveAiSettingsCommand, AiSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    public sealed record AllowanceInput(int? MonthlyGenerations);

    private static async Task<Results<Ok<AiUsageRow>, ProblemHttpResult>> Allowance(Guid userId, AllowanceInput body, ICommandHandler<SetAiAllowanceCommand, AiUsageRow> h, CancellationToken ct) =>
        (await h.Handle(new SetAiAllowanceCommand(userId, body.MonthlyGenerations), ct)).ToHttpResult();

    private static async Task<Results<Ok<AiUsage>, ProblemHttpResult>> Usage([AsParameters] GetAiUsageQuery query, IQueryHandler<GetAiUsageQuery, AiUsage> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AttemptRows>, ProblemHttpResult>> Attempts(Guid id, IQueryHandler<ListTestAttemptsQuery, AttemptRows> h, CancellationToken ct) =>
        (await h.Handle(new ListTestAttemptsQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<QuestionResponses>, ProblemHttpResult>> Responses(Guid id, Guid questionId, IQueryHandler<ListQuestionResponsesQuery, QuestionResponses> h, CancellationToken ct) =>
        (await h.Handle(new ListQuestionResponsesQuery(id, questionId), ct)).ToHttpResult();

    private static async Task<Results<Ok<AttemptReview>, ProblemHttpResult>> Review(Guid id, IQueryHandler<GetAttemptReviewQuery, AttemptReview> h, CancellationToken ct) =>
        (await h.Handle(new GetAttemptReviewQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<AttemptReview>, ProblemHttpResult>> Mark(Guid id, Guid itemId, MarkInput body, ICommandHandler<MarkAnswerCommand, AttemptReview> h, CancellationToken ct) =>
        (await h.Handle(new MarkAnswerCommand(id, itemId, body), ct)).ToHttpResult();

    private static async Task<Results<Ok<TestDetail>, ProblemHttpResult>> Release(Guid id, ICommandHandler<ReleaseResultsCommand, TestDetail> h, CancellationToken ct) =>
        (await h.Handle(new ReleaseResultsCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<GradebookPreview>, ProblemHttpResult>> GradebookPreview(Guid id, IQueryHandler<PreviewGradebookPushQuery, GradebookPreview> h, CancellationToken ct) =>
        (await h.Handle(new PreviewGradebookPushQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<PushResult>, ProblemHttpResult>> PushGradebook(Guid id, PushInput body, ICommandHandler<PushToGradebookCommand, PushResult> h, CancellationToken ct) =>
        (await h.Handle(new PushToGradebookCommand(id, body.StudentIds), ct)).ToHttpResult();

    private static async Task<Results<Ok<TestAnalytics>, ProblemHttpResult>> Analytics(Guid id, IQueryHandler<GetTestAnalyticsQuery, TestAnalytics> h, CancellationToken ct) =>
        (await h.Handle(new GetTestAnalyticsQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<TestSummary>>, ProblemHttpResult>> Tests([AsParameters] ListTestsQuery query, IQueryHandler<ListTestsQuery, IReadOnlyList<TestSummary>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<TestDetail>, ProblemHttpResult>> Test(Guid id, IQueryHandler<GetTestQuery, TestDetail> h, CancellationToken ct) =>
        (await h.Handle(new GetTestQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<TestDetail>, ProblemHttpResult>> CreateTest(TestInput body, ICommandHandler<SaveTestCommand, TestDetail> h, CancellationToken ct) =>
        (await h.Handle(new SaveTestCommand(null, body), ct)).ToHttpResult();

    private static async Task<Results<Ok<TestDetail>, ProblemHttpResult>> UpdateTest(Guid id, TestInput body, ICommandHandler<SaveTestCommand, TestDetail> h, CancellationToken ct) =>
        (await h.Handle(new SaveTestCommand(id, body), ct)).ToHttpResult();

    private static async Task<Results<Ok<TestDetail>, ProblemHttpResult>> Sections(Guid id, SectionsInput body, ICommandHandler<SaveTestSectionsCommand, TestDetail> h, CancellationToken ct) =>
        (await h.Handle(new SaveTestSectionsCommand(id, body.Sections ?? []), ct)).ToHttpResult();

    private static async Task<Results<Ok<TestDetail>, ProblemHttpResult>> Publish(Guid id, ICommandHandler<PublishTestCommand, TestDetail> h, CancellationToken ct) =>
        (await h.Handle(new PublishTestCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<TestDetail>, ProblemHttpResult>> Unpublish(Guid id, ICommandHandler<PublishTestCommand, TestDetail> h, CancellationToken ct) =>
        (await h.Handle(new PublishTestCommand(id, Publish: false), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteTest(Guid id, ICommandHandler<DeleteTestCommand> h, CancellationToken ct) =>
        (await h.Handle(new DeleteTestCommand(id), ct)).ToHttpResult();

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
