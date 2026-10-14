using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Application.Photos.GetStudentPhoto;
using Maydel.Application.Portal;
using Maydel.Application.Portal.GetChildResult;
using Maydel.Application.Portal.GetChildResultPdf;
using Maydel.Application.Portal.ListChildResults;
using Maydel.Application.Portal.ListMyChildren;
using Maydel.Application.Reports;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Parent & student portal (M1.3, E6.1–E6.2). Parents see their active children; students see themselves.
/// Only published results are visible. Another family's child, or a draft, is a 404 so nothing can be probed.
/// </summary>
public static class PortalEndpoints
{
    public static void MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var portal = app.MapGroup("/api/portal").WithTags("Portal").RequireAuthorization(Policies.Portal);

        portal.MapGet("/children", ListChildren).WithName("ListMyChildren").ProducesProblems(401, 403);
        portal.MapGet("/children/{studentId:guid}/photo", GetPhoto).WithName("GetChildPhoto").ProducesProblems(401, 403, 404).PrivateCache();
        portal.MapGet("/children/{studentId:guid}/results", ListResults).WithName("ListChildResults").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/results/{termId:guid}", GetResult).WithName("GetChildResult").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/results/{termId:guid}/pdf", GetResultPdf).WithName("GetChildResultPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);
    }

    private static async Task<Results<Ok<IReadOnlyList<PortalChild>>, ProblemHttpResult>> ListChildren(
        IQueryHandler<ListMyChildrenQuery, IReadOnlyList<PortalChild>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListMyChildrenQuery(), ct)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> GetPhoto(
        Guid studentId, IQueryHandler<GetStudentPhotoQuery, PhotoContent> handler, CancellationToken ct) =>
        PhotoEndpoints.ToFile(await handler.Handle(new GetStudentPhotoQuery(studentId), ct));

    private static async Task<Results<Ok<IReadOnlyList<PortalResultSummary>>, ProblemHttpResult>> ListResults(
        Guid studentId, IQueryHandler<ListChildResultsQuery, IReadOnlyList<PortalResultSummary>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListChildResultsQuery(studentId), ct)).ToHttpResult();

    private static async Task<Results<Ok<ReportCard>, ProblemHttpResult>> GetResult(
        Guid studentId, Guid termId, IQueryHandler<GetChildResultQuery, ReportCard> handler, CancellationToken ct) =>
        (await handler.Handle(new GetChildResultQuery(studentId, termId), ct)).ToHttpResult();

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GetResultPdf(
        Guid studentId, Guid termId, IQueryHandler<GetChildResultPdfQuery, PdfFile> handler, CancellationToken ct)
    {
        var result = await handler.Handle(new GetChildResultPdfQuery(studentId, termId), ct);
        return result.IsSuccess ? TypedResults.File(result.Value.Content, "application/pdf", result.Value.FileName) : result.Error!.ToProblem();
    }
}
