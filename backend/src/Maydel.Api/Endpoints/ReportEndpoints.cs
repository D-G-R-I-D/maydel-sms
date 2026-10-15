using Maydel.Api.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;
using Maydel.Application.Common;
using Maydel.Application.Reports.GetClassReportCardsPdf;
using Maydel.Application.Reports.GetClassReports;
using Maydel.Application.Reports.GetReportCardPdf;
using Maydel.Application.Reports.GetReportCard;
using Maydel.Application.Notifications;
using Maydel.Application.Notifications.ListReportEmails;
using Maydel.Application.Notifications.RetryEmail;
using Maydel.Application.Reports.PublishClassReports;
using Maydel.Application.Reports.SaveAttendance;
using Maydel.Application.Reports.SetBehaviour;
using Maydel.Application.Reports.SetFormTeacherComment;
using Maydel.Application.Reports.SetPrincipalComment;
using Maydel.Application.Reports.UnpublishClassReports;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Term reports (E5.3–E5.5, E2.5) for staff: class list, report card preview, comments, behaviour, attendance, publish/unpublish.
/// Viewing: admins and the class's teachers. Form-teacher actions: that class's form teacher or an admin. Publishing: admins.
/// Parents and students read published reports through the portal (M1.3), not here.
/// </summary>
public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var academic = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization(Policies.Academic);
        var admin = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization(Policies.Admin);

        academic.MapGet("/class", GetClass).WithName("GetClassReports").ProducesProblems(400, 401, 403, 404);
        academic.MapGet("/{studentId:guid}/{termId:guid}", GetCard).WithName("GetReportCard").ProducesProblems(401, 403, 404, 422);
        academic.MapGet("/{studentId:guid}/{termId:guid}/pdf", GetCardPdf).WithName("GetReportCardPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404, 422);
        academic.MapGet("/class/pdf", GetClassPdf).WithName("GetClassReportCardsPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(400, 401, 403, 404, 422);
        academic.MapPut("/{studentId:guid}/{termId:guid}/form-teacher-comment", SetFormTeacherComment).WithName("SetFormTeacherComment").ProducesProblems(400, 401, 403, 404, 422);
        academic.MapPut("/{studentId:guid}/{termId:guid}/behaviour", SetBehaviour).WithName("SetReportBehaviour").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapPut("/{studentId:guid}/{termId:guid}/principal-comment", SetPrincipalComment).WithName("SetPrincipalComment").ProducesProblems(400, 401, 403, 404, 422);
        academic.MapPut("/attendance", SaveAttendance).WithName("SaveReportAttendance").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapPost("/publish", Publish).WithName("PublishClassReports").ProducesProblems(401, 403, 404, 422);
        admin.MapGet("/emails", ListEmails).WithName("ListReportEmails").ProducesProblems(400, 401, 403);
        admin.MapPost("/emails/{emailId:guid}/retry", RetryEmail).WithName("RetryReportEmail").ProducesProblems(401, 403, 404, 422);
        admin.MapPost("/unpublish", Unpublish).WithName("UnpublishClassReports").ProducesProblems(401, 403, 404);
    }

    private static async Task<Results<Ok<ClassReportSummary>, ProblemHttpResult>> GetClass(
        [AsParameters] GetClassReportsQuery query, IQueryHandler<GetClassReportsQuery, ClassReportSummary> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<ReportCard>, ProblemHttpResult>> GetCard(
        Guid studentId, Guid termId, IQueryHandler<GetReportCardQuery, ReportCard> handler, CancellationToken ct) =>
        (await handler.Handle(new GetReportCardQuery(studentId, termId), ct)).ToHttpResult();

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GetCardPdf(
        Guid studentId, Guid termId, IQueryHandler<GetReportCardPdfQuery, PdfFile> handler, CancellationToken ct) =>
        ToPdf(await handler.Handle(new GetReportCardPdfQuery(studentId, termId), ct));

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GetClassPdf(
        [AsParameters] GetClassReportCardsPdfQuery query, IQueryHandler<GetClassReportCardsPdfQuery, PdfFile> handler, CancellationToken ct) =>
        ToPdf(await handler.Handle(query, ct));

    /// <summary>Inline so browsers preview it; the file name is used when saving.</summary>
    private static Results<FileContentHttpResult, ProblemHttpResult> ToPdf(Result<PdfFile> result) =>
        result.IsSuccess ? TypedResults.File(result.Value.Content, "application/pdf", result.Value.FileName) : result.Error!.ToProblem();

    private static async Task<Results<Ok<ReportCard>, ProblemHttpResult>> SetFormTeacherComment(
        Guid studentId, Guid termId, CommentRequest body, ICommandHandler<SetFormTeacherCommentCommand, ReportCard> handler, CancellationToken ct) =>
        (await handler.Handle(new SetFormTeacherCommentCommand(studentId, termId, body.Comment), ct)).ToHttpResult();

    private static async Task<Results<Ok<ReportCard>, ProblemHttpResult>> SetBehaviour(
        Guid studentId, Guid termId, BehaviourRequest body, ICommandHandler<SetBehaviourCommand, ReportCard> handler, CancellationToken ct) =>
        (await handler.Handle(new SetBehaviourCommand(studentId, termId, body.Ratings ?? [], body.Clubs ?? []), ct)).ToHttpResult();

    private static async Task<Results<Ok<ReportCard>, ProblemHttpResult>> SetPrincipalComment(
        Guid studentId, Guid termId, CommentRequest body, ICommandHandler<SetPrincipalCommentCommand, ReportCard> handler, CancellationToken ct) =>
        (await handler.Handle(new SetPrincipalCommentCommand(studentId, termId, body.Comment), ct)).ToHttpResult();

    private static async Task<Results<Ok<ClassReportSummary>, ProblemHttpResult>> SaveAttendance(
        SaveAttendanceCommand command, ICommandHandler<SaveAttendanceCommand, ClassReportSummary> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<PublishResult>, ProblemHttpResult>> Publish(
        PublishClassReportsCommand command, ICommandHandler<PublishClassReportsCommand, PublishResult> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ReportEmailStatus>>, ProblemHttpResult>> ListEmails(
        [AsParameters] ListReportEmailsQuery query, IQueryHandler<ListReportEmailsQuery, IReadOnlyList<ReportEmailStatus>> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> RetryEmail(
        Guid emailId, ICommandHandler<RetryEmailCommand> handler, CancellationToken ct) =>
        (await handler.Handle(new RetryEmailCommand(emailId), ct)).ToHttpResult();

    private static async Task<Results<Ok<UnpublishResult>, ProblemHttpResult>> Unpublish(
        UnpublishClassReportsCommand command, ICommandHandler<UnpublishClassReportsCommand, UnpublishResult> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();
}
