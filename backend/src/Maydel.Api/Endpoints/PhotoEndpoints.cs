using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Application.Photos.GetStaffPhoto;
using Maydel.Application.Photos.GetStudentPhoto;
using Maydel.Application.Photos.RemoveStaffPhoto;
using Maydel.Application.Photos.RemoveStudentPhoto;
using Maydel.Application.Photos.SetStaffPhoto;
using Maydel.Application.Photos.SetStudentPhoto;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Student and staff photos (E3.1, E4.1). Uploads are multipart field <c>file</c>; images are re-encoded server-side.
/// Viewing applies the record's own access rules; responses are private-cacheable and keyed by version (ETag).
/// </summary>
public static class PhotoEndpoints
{
    public static void MapPhotoEndpoints(this IEndpointRouteBuilder app)
    {
        var students = app.MapGroup("/api/students/{id:guid}/photo").WithTags("Photos");
        // Viewing goes through AccessRules (staff: any student; later the portal: own children / self).
        students.MapGet("", GetStudentPhoto).RequireAuthorization(Policies.Staff).WithName("GetStudentPhoto").ProducesProblems(401, 403, 404).PrivateCache();
        students.MapPut("", SetStudentPhoto).RequireAuthorization(Policies.Admin).WithName("SetStudentPhoto").Upload();
        students.MapDelete("", RemoveStudentPhoto).RequireAuthorization(Policies.Admin).WithName("RemoveStudentPhoto").ProducesProblems(401, 403, 404);

        var staff = app.MapGroup("/api/staff/{id:guid}/photo").WithTags("Photos");
        staff.MapGet("", GetStaffPhoto).RequireAuthorization(Policies.Staff).WithName("GetStaffPhoto").ProducesProblems(401, 403, 404).PrivateCache();
        staff.MapPut("", SetStaffPhoto).RequireAuthorization(Policies.Admin).WithName("SetStaffPhoto").Upload();
        staff.MapDelete("", RemoveStaffPhoto).RequireAuthorization(Policies.Admin).WithName("RemoveStaffPhoto").ProducesProblems(401, 403, 404);
    }

    /// <summary>
    /// Successful photo responses may be cached by this user's browser only (never shared caches). Clients add the
    /// record's <c>photoVersion</c> as <c>?v=</c>, so a replaced photo is a new URL and long caching is safe.
    /// </summary>
    internal static RouteHandlerBuilder PrivateCache(this RouteHandlerBuilder builder) => builder.AddEndpointFilter(async (context, next) =>
    {
        var response = context.HttpContext.Response;
        response.OnStarting(() =>
        {
            response.Headers.CacheControl = response.StatusCode is 200 or 304 ? "private, max-age=86400" : "no-store";
            return Task.CompletedTask;
        });
        return await next(context);
    });

    internal static RouteHandlerBuilder Upload(this RouteHandlerBuilder builder) => builder
        // Bearer-token API: no cookies, so there's no CSRF to defend against on multipart posts.
        .DisableAntiforgery()
        .WithMetadata(new RequestSizeLimitAttribute(PhotoStore.MaxUploadBytes + 64 * 1024))
        .Accepts<IFormFile>("multipart/form-data")
        .ProducesProblems(400, 401, 403, 404);

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> GetStudentPhoto(
        Guid id, IQueryHandler<GetStudentPhotoQuery, PhotoContent> handler, CancellationToken ct) =>
        ToFile(await handler.Handle(new GetStudentPhotoQuery(id), ct));

    private static async Task<Results<Ok<PhotoInfo>, ProblemHttpResult>> SetStudentPhoto(
        Guid id, IFormFile? file, ICommandHandler<SetStudentPhotoCommand, PhotoInfo> handler, CancellationToken ct) =>
        await ReadAsync(file, ct) is { } bytes
            ? (await handler.Handle(new SetStudentPhotoCommand(id, bytes), ct)).ToHttpResult()
            : PhotoErrors.Invalid.ToProblem();

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveStudentPhoto(
        Guid id, ICommandHandler<RemoveStudentPhotoCommand> handler, CancellationToken ct) =>
        (await handler.Handle(new RemoveStudentPhotoCommand(id), ct)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> GetStaffPhoto(
        Guid id, IQueryHandler<GetStaffPhotoQuery, PhotoContent> handler, CancellationToken ct) =>
        ToFile(await handler.Handle(new GetStaffPhotoQuery(id), ct));

    private static async Task<Results<Ok<PhotoInfo>, ProblemHttpResult>> SetStaffPhoto(
        Guid id, IFormFile? file, ICommandHandler<SetStaffPhotoCommand, PhotoInfo> handler, CancellationToken ct) =>
        await ReadAsync(file, ct) is { } bytes
            ? (await handler.Handle(new SetStaffPhotoCommand(id, bytes), ct)).ToHttpResult()
            : PhotoErrors.Invalid.ToProblem();

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveStaffPhoto(
        Guid id, ICommandHandler<RemoveStaffPhotoCommand> handler, CancellationToken ct) =>
        (await handler.Handle(new RemoveStaffPhotoCommand(id), ct)).ToHttpResult();

    /// <summary>The upload's bytes, or null when missing or over the size cap. Content checks happen in the image processor.</summary>
    internal static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > PhotoStore.MaxUploadBytes) return null;
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    internal static Results<FileStreamHttpResult, ProblemHttpResult> ToFile(Result<PhotoContent> result)
    {
        if (result.IsFailure) return result.Error!.ToProblem();
        var photo = result.Value;
        // Private: only this signed-in user's browser (via the BFF) may cache it. Versioned URLs make long caching safe.
        return TypedResults.File(photo.Content, photo.ContentType, entityTag: new EntityTagHeaderValue(photo.ETag), lastModified: null);
    }
}
