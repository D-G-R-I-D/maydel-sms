using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Communication;
using Maydel.Application.Homework;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Maydel.Api.Endpoints;

/// <summary>
/// The communication book (E17.2): one notebook per student between the school (form teacher, admins) and the family
/// (guardians, the student). The handlers decide who may open which book; anyone else gets a 404.
/// </summary>
public static class CommunicationBookEndpoints
{
    public static void MapCommunicationBookEndpoints(this IEndpointRouteBuilder app)
    {
        var school = app.MapGroup("/api/books").WithTags("Communication book").RequireAuthorization(Policies.Academic);
        school.MapGet("", ClassBooks).WithName("ListClassBooks").ProducesProblems(401, 403);

        var mine = app.MapGroup("/api/portal/books").WithTags("Communication book").RequireAuthorization(Policies.Portal);
        mine.MapGet("", MyBooks).WithName("ListMyBooks").ProducesProblems(401, 403);

        var book = app.MapGroup("/api/books/{studentId:guid}").WithTags("Communication book").RequireAuthorization();
        book.MapGet("", Get).WithName("GetBook").ProducesProblems(401, 404);
        book.MapPost("/entries", Write).WithName("WriteBookEntry").DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(HomeworkFiles.MaxBytes + 256 * 1024)).Accepts<IFormFile>("multipart/form-data").ProducesProblems(400, 401, 404);
        book.MapPut("/entries/{entryId:guid}", Edit).WithName("EditBookEntry").ProducesProblems(400, 401, 403, 404, 422);
        book.MapPost("/read", Read).WithName("MarkBookRead").ProducesProblems(401, 404);
        book.MapGet("/entries/{entryId:guid}/file", File).WithName("GetBookEntryFile").ProducesProblems(401, 404);
    }

    private static async Task<Results<Ok<IReadOnlyList<BookSummary>>, ProblemHttpResult>> ClassBooks(Guid? classRoomId, IQueryHandler<ListClassBooksQuery, IReadOnlyList<BookSummary>> h, CancellationToken ct) =>
        (await h.Handle(new ListClassBooksQuery(classRoomId), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<BookSummary>>, ProblemHttpResult>> MyBooks(IQueryHandler<ListMyBooksQuery, IReadOnlyList<BookSummary>> h, CancellationToken ct) =>
        (await h.Handle(new ListMyBooksQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<BookPage>, ProblemHttpResult>> Get(Guid studentId, DateTimeOffset? before, IQueryHandler<GetBookQuery, BookPage> h, CancellationToken ct) =>
        (await h.Handle(new GetBookQuery(studentId, before), ct)).ToHttpResult();

    private static async Task<Results<Ok<BookEntryDto>, ProblemHttpResult>> Write(Guid studentId, HttpRequest request, ICommandHandler<WriteEntryCommand, BookEntryDto> h, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Error.Validation("body", "Write a note or attach a file.").ToProblem();
        var form = await request.ReadFormAsync(ct);
        byte[]? bytes = null;
        string? name = null;
        if (form.Files.GetFile("file") is { } file)
        {
            if (file.Length is 0 or > HomeworkFiles.MaxBytes) return HomeworkFiles.Invalid.ToProblem();
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            (bytes, name) = (buffer.ToArray(), file.FileName);
        }
        return (await h.Handle(new WriteEntryCommand(studentId, form["body"].ToString(), bytes, name), ct)).ToHttpResult();
    }

    private static async Task<Results<Ok<BookEntryDto>, ProblemHttpResult>> Edit(Guid studentId, Guid entryId, EditEntryInput body, ICommandHandler<EditEntryCommand, BookEntryDto> h, CancellationToken ct) =>
        (await h.Handle(new EditEntryCommand(studentId, entryId, body.Body), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> Read(Guid studentId, ICommandHandler<MarkBookReadCommand> h, CancellationToken ct) =>
        (await h.Handle(new MarkBookReadCommand(studentId), ct)).ToHttpResult();
    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> File(Guid studentId, Guid entryId, IQueryHandler<GetBookFileQuery, HomeworkFileContent> h, CancellationToken ct)
    {
        var result = await h.Handle(new GetBookFileQuery(studentId, entryId), ct);
        return result.IsSuccess ? TypedResults.File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Error!.ToProblem();
    }
}
