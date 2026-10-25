using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Homework;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Homework (E17.1). Teachers set it for classes and subjects they teach and review hand-ins; students hand in from the
/// portal; parents follow along. Another class's homework is a 404.
/// </summary>
public static class HomeworkEndpoints
{
    /// <summary>Up to 5 files of 10 MB each, plus form fields.</summary>
    private const long MaxRequest = 5L * HomeworkFiles.MaxBytes + 256 * 1024;

    public static void MapHomeworkEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/api/homework").WithTags("Homework").RequireAuthorization(Policies.Academic);
        staff.MapGet("", List).WithName("ListHomework").ProducesProblems(400, 401, 403);
        staff.MapGet("/{id:guid}", Get).WithName("GetHomework").ProducesProblems(401, 403, 404);
        staff.MapPost("", Create).WithName("CreateHomework").ProducesProblems(400, 401, 403);
        staff.MapPut("/{id:guid}", Update).WithName("UpdateHomework").ProducesProblems(400, 401, 403, 404);
        staff.MapDelete("/{id:guid}", Delete).WithName("DeleteHomework").ProducesProblems(401, 403, 404, 422);
        staff.MapPost("/{id:guid}/files", AddFile).WithName("AddHomeworkFile").DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(HomeworkFiles.MaxBytes + 256 * 1024)).Accepts<IFormFile>("multipart/form-data").ProducesProblems(400, 401, 403, 404, 422);
        staff.MapDelete("/{id:guid}/files/{fileId:guid}", RemoveFile).WithName("RemoveHomeworkFile").ProducesProblems(401, 403, 404);
        staff.MapGet("/{id:guid}/hand-ins", HandIns).WithName("ListHandIns").ProducesProblems(401, 403, 404);
        staff.MapPut("/{id:guid}/hand-ins/{submissionId:guid}", Review).WithName("ReviewHandIn").ProducesProblems(400, 401, 403, 404);

        // Files: any signed-in user; the handler checks the row (teacher of the class, or the family).
        var files = app.MapGroup("/api/homework").WithTags("Homework").RequireAuthorization();
        files.MapGet("/{id:guid}/files/{fileId:guid}", File).WithName("GetHomeworkFile").ProducesProblems(401, 404);
        files.MapGet("/{id:guid}/hand-ins/{submissionId:guid}/files/{fileId:guid}", HandInFile).WithName("GetHandInFile").ProducesProblems(401, 404);

        var portal = app.MapGroup("/api/portal/children/{studentId:guid}/homework").WithTags("Portal").RequireAuthorization(Policies.Portal);
        portal.MapGet("", Mine).WithName("ListChildHomework").ProducesProblems(401, 403, 404);
        portal.MapGet("/{homeworkId:guid}", MineOne).WithName("GetChildHomework").ProducesProblems(401, 403, 404);
        portal.MapPost("/{homeworkId:guid}/hand-in", HandIn).WithName("HandInHomework").DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequest)).Accepts<IFormFile>("multipart/form-data").ProducesProblems(400, 401, 403, 404, 422);
    }

    private static async Task<Results<Ok<PagedResult<HomeworkDto>>, ProblemHttpResult>> List([AsParameters] ListHomeworkQuery q, IQueryHandler<ListHomeworkQuery, PagedResult<HomeworkDto>> h, CancellationToken ct) =>
        (await h.Handle(q, ct)).ToHttpResult();
    private static async Task<Results<Ok<HomeworkDto>, ProblemHttpResult>> Get(Guid id, IQueryHandler<GetHomeworkQuery, HomeworkDto> h, CancellationToken ct) =>
        (await h.Handle(new GetHomeworkQuery(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<HomeworkDto>, ProblemHttpResult>> Create(HomeworkInput body, ICommandHandler<SaveHomeworkCommand, HomeworkDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveHomeworkCommand(null, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<HomeworkDto>, ProblemHttpResult>> Update(Guid id, HomeworkInput body, ICommandHandler<SaveHomeworkCommand, HomeworkDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveHomeworkCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ICommandHandler<DeleteHomeworkCommand> h, CancellationToken ct) =>
        (await h.Handle(new DeleteHomeworkCommand(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<HomeworkDto>, ProblemHttpResult>> AddFile(Guid id, IFormFile? file, ICommandHandler<AddHomeworkFileCommand, HomeworkDto> h, CancellationToken ct) =>
        await ReadAsync(file, ct) is { } bytes ? (await h.Handle(new AddHomeworkFileCommand(id, bytes, file!.FileName), ct)).ToHttpResult() : HomeworkFiles.Invalid.ToProblem();
    private static async Task<Results<Ok<HomeworkDto>, ProblemHttpResult>> RemoveFile(Guid id, Guid fileId, ICommandHandler<RemoveHomeworkFileCommand, HomeworkDto> h, CancellationToken ct) =>
        (await h.Handle(new RemoveHomeworkFileCommand(id, fileId), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<RosterRow>>, ProblemHttpResult>> HandIns(Guid id, IQueryHandler<ListHandInsQuery, IReadOnlyList<RosterRow>> h, CancellationToken ct) =>
        (await h.Handle(new ListHandInsQuery(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<SubmissionDto>, ProblemHttpResult>> Review(Guid id, Guid submissionId, ReviewInput body, ICommandHandler<ReviewHandInCommand, SubmissionDto> h, CancellationToken ct) =>
        (await h.Handle(new ReviewHandInCommand(id, submissionId, body), ct)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> File(Guid id, Guid fileId, IQueryHandler<GetHomeworkFileQuery, HomeworkFileContent> h, CancellationToken ct) =>
        Download(await h.Handle(new GetHomeworkFileQuery(id, fileId), ct));
    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> HandInFile(Guid id, Guid submissionId, Guid fileId, IQueryHandler<GetHandInFileQuery, HomeworkFileContent> h, CancellationToken ct) =>
        Download(await h.Handle(new GetHandInFileQuery(id, submissionId, fileId), ct));

    private static async Task<Results<Ok<IReadOnlyList<MyHomework>>, ProblemHttpResult>> Mine(Guid studentId, bool? open, IQueryHandler<ListChildHomeworkQuery, IReadOnlyList<MyHomework>> h, CancellationToken ct) =>
        (await h.Handle(new ListChildHomeworkQuery(studentId, open), ct)).ToHttpResult();
    private static async Task<Results<Ok<MyHomework>, ProblemHttpResult>> MineOne(Guid studentId, Guid homeworkId, IQueryHandler<GetChildHomeworkQuery, MyHomework> h, CancellationToken ct) =>
        (await h.Handle(new GetChildHomeworkQuery(studentId, homeworkId), ct)).ToHttpResult();

    private static async Task<Results<Ok<MyHomework>, ProblemHttpResult>> HandIn(Guid studentId, Guid homeworkId, HttpRequest request, ICommandHandler<HandInCommand, MyHomework> h, CancellationToken ct)
    {
        if (!request.HasFormContentType) return HomeworkErrors.EmptySubmission.ToProblem();
        var form = await request.ReadFormAsync(ct);
        var files = new List<(byte[], string?)>();
        foreach (var f in form.Files.Where(f => f.Name == "files"))
        {
            if (await ReadAsync(f, ct) is not { } bytes) return HomeworkFiles.Invalid.ToProblem();
            files.Add((bytes, f.FileName));
        }
        return (await h.Handle(new HandInCommand(studentId, homeworkId, form["text"].ToString(), files), ct)).ToHttpResult();
    }

    private static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > HomeworkFiles.MaxBytes) return null;
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    /// <summary>Always a download (never shown inline), with the stored, cleaned file name.</summary>
    private static Results<FileStreamHttpResult, ProblemHttpResult> Download(Result<HomeworkFileContent> result) =>
        result.IsSuccess ? TypedResults.File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Error!.ToProblem();
}
