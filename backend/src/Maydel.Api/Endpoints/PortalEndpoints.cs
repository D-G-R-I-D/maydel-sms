using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Application.Photos.GetStudentPhoto;
using Maydel.Application.Portal;
using Maydel.Application.Portal.GetChildFeeDocument;
using Maydel.Application.Portal.GetChildFees;
using Maydel.Application.Portal.GetOnlinePayment;
using Maydel.Application.Portal.SubmitPaymentProof;
using Maydel.Application.Portal.ListChildProofs;
using Maydel.Application.Portal.GetChildProofFile;
using Microsoft.AspNetCore.Mvc;
using Maydel.Application.Portal.StartOnlinePayment;
using Maydel.Application.Finance;
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
        portal.MapGet("/children/{studentId:guid}/fees", GetFees).WithName("GetChildFees").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/invoices/{invoiceId:guid}/pdf", GetInvoicePdf).WithName("GetChildInvoicePdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/receipts/{paymentId:guid}/pdf", GetReceiptPdf).WithName("GetChildReceiptPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);
        portal.MapPost("/children/{studentId:guid}/invoices/{invoiceId:guid}/pay", StartPayment).WithName("StartOnlinePayment").ProducesProblems(400, 401, 403, 404, 422);
        portal.MapPost("/children/{studentId:guid}/invoices/{invoiceId:guid}/proofs", SubmitProof).WithName("SubmitPaymentProof")
            .DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(PhotoStore.MaxUploadBytes + 64 * 1024))
            .Accepts<IFormFile>("multipart/form-data").ProducesProblems(400, 401, 403, 404, 422);
        portal.MapGet("/children/{studentId:guid}/proofs", ListProofs).WithName("ListChildProofs").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/proofs/{proofId:guid}/file", ProofFile).WithName("GetChildProofFile").ProducesProblems(401, 403, 404);
        portal.MapGet("/payments/{reference}", GetPayment).WithName("GetOnlinePayment").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/tests", ListTests).WithName("ListChildTests").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/results", ListResults).WithName("ListChildResults").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/results/{termId:guid}", GetResult).WithName("GetChildResult").ProducesProblems(401, 403, 404);
        portal.MapGet("/children/{studentId:guid}/results/{termId:guid}/pdf", GetResultPdf).WithName("GetChildResultPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);
    }

    public sealed record PayRequest(decimal? Amount);

    private static async Task<Results<Ok<IReadOnlyList<Application.Cbt.ChildTestResult>>, ProblemHttpResult>> ListTests(
        Guid studentId, IQueryHandler<Application.Portal.ListChildTests.ListChildTestsQuery, IReadOnlyList<Application.Cbt.ChildTestResult>> h, CancellationToken ct) =>
        (await h.Handle(new Application.Portal.ListChildTests.ListChildTestsQuery(studentId), ct)).ToHttpResult();

    private static async Task<Results<Ok<ProofDto>, ProblemHttpResult>> SubmitProof(
        Guid studentId, Guid invoiceId, [FromForm] decimal amount, [FromForm] DateOnly paidOn, [FromForm] string? bankReference, [FromForm] string? note,
        IFormFile? file, ICommandHandler<SubmitPaymentProofCommand, ProofDto> handler, CancellationToken ct)
    {
        if (await PhotoEndpoints.ReadAsync(file, ct) is not { } bytes) return FinanceErrors.ProofFile.ToProblem();
        return (await handler.Handle(new SubmitPaymentProofCommand(studentId, invoiceId, amount, paidOn, bankReference, note, bytes), ct)).ToHttpResult();
    }

    private static async Task<Results<Ok<IReadOnlyList<ProofDto>>, ProblemHttpResult>> ListProofs(
        Guid studentId, IQueryHandler<ListChildProofsQuery, IReadOnlyList<ProofDto>> handler, CancellationToken ct) =>
        (await handler.Handle(new ListChildProofsQuery(studentId), ct)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> ProofFile(
        Guid studentId, Guid proofId, IQueryHandler<GetChildProofFileQuery, PhotoContent> handler, CancellationToken ct) =>
        FinanceEndpoints.ProofDownload(await handler.Handle(new GetChildProofFileQuery(studentId, proofId), ct));

    private static async Task<Results<Ok<OnlineCheckout>, ProblemHttpResult>> StartPayment(
        Guid studentId, Guid invoiceId, PayRequest body, ICommandHandler<StartOnlinePaymentCommand, OnlineCheckout> handler, CancellationToken ct) =>
        (await handler.Handle(new StartOnlinePaymentCommand(studentId, invoiceId, body.Amount), ct)).ToHttpResult();

    private static async Task<Results<Ok<OnlinePaymentResult>, ProblemHttpResult>> GetPayment(
        string reference, IQueryHandler<GetOnlinePaymentQuery, OnlinePaymentResult> handler, CancellationToken ct) =>
        (await handler.Handle(new GetOnlinePaymentQuery(reference), ct)).ToHttpResult();

    private static async Task<Results<Ok<PortalFees>, ProblemHttpResult>> GetFees(
        Guid studentId, IQueryHandler<GetChildFeesQuery, PortalFees> handler, CancellationToken ct) =>
        (await handler.Handle(new GetChildFeesQuery(studentId), ct)).ToHttpResult();

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GetInvoicePdf(
        Guid studentId, Guid invoiceId, IQueryHandler<GetChildFeeDocumentQuery, PdfFile> handler, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await handler.Handle(new GetChildFeeDocumentQuery(studentId, invoiceId, null), ct));

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GetReceiptPdf(
        Guid studentId, Guid paymentId, IQueryHandler<GetChildFeeDocumentQuery, PdfFile> handler, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await handler.Handle(new GetChildFeeDocumentQuery(studentId, null, paymentId), ct));

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
