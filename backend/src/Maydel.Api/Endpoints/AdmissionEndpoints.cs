using Maydel.Api.Common;
using Maydel.Application.Admissions;
using System.Globalization;
using System.Text;
using Maydel.Application.Admissions.BookExam;
using Maydel.Application.Admissions.DeleteExamSlot;
using Maydel.Application.Admissions.GetExamResults;
using Maydel.Application.Admissions.ListBookableSlots;
using Maydel.Application.Admissions.ListExamCandidates;
using Maydel.Application.Admissions.ListExamSlots;
using Maydel.Application.Admissions.MarkExamAbsent;
using Maydel.Application.Admissions.NotifyStageOnePasses;
using Maydel.Application.Admissions.RecordExamScore;
using Maydel.Application.Admissions.ReopenApplication;
using Maydel.Application.Admissions.SaveExamSlot;
using Maydel.Application.Admissions.SetCutOff;
using Maydel.Application.Admissions.GetAdmissionSettings;
using Maydel.Application.Admissions.GetApplication;
using Maydel.Application.Admissions.GetApplicationDocument;
using Maydel.Application.Admissions.GetApplicationFormPdf;
using Maydel.Application.Admissions.RemoveApplicationDocument;
using Maydel.Application.Admissions.SaveApplicationForm;
using Maydel.Application.Admissions.SubmitApplication;
using Maydel.Application.Admissions.UploadApplicationDocument;
using Maydel.Application.Photos;
using Maydel.Application.Reports;
using Maydel.Domain.Admissions;
using Microsoft.AspNetCore.Mvc;
using Maydel.Application.Admissions.GetAdmissionsInfo;
using Maydel.Application.Admissions.GetFormPayment;
using Maydel.Application.Admissions.IssueFormCodes;
using Maydel.Application.Admissions.ListApplications;
using Maydel.Application.Admissions.ListFormCodes;
using Maydel.Application.Admissions.ListMyApplications;
using Maydel.Application.Admissions.RedeemFormCode;
using Maydel.Application.Admissions.Register;
using Maydel.Application.Admissions.SaveAdmissionSettings;
using Maydel.Application.Admissions.StartApplication;
using Maydel.Application.Admissions.StartFormPayment;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Admissions, part 1 (E11.4): the public "Buy admission form" page, the family's applicant account and applications,
/// paying for the form (online or with a school-issued code), and the staff side (settings, codes, list).
/// </summary>
public static class AdmissionEndpoints
{
    public static void MapAdmissionEndpoints(this IEndpointRouteBuilder app)
    {
        var open = app.MapGroup("/api/public/admissions").WithTags("Admissions").AllowAnonymous();
        var family = app.MapGroup("/api/admissions").WithTags("Admissions").RequireAuthorization(Policies.Applicants);
        var finance = app.MapGroup("/api/admissions").WithTags("Admissions").RequireAuthorization(Policies.Finance);
        var admin = app.MapGroup("/api/admissions").WithTags("Admissions").RequireAuthorization(Policies.Admin);
        var staff = app.MapGroup("/api/admissions").WithTags("Admissions").RequireAuthorization(Policies.Staff);
        var academic = app.MapGroup("/api/admissions").WithTags("Admissions").RequireAuthorization(Policies.Academic);

        // Entrance exam (E11.6–7)
        family.MapGet("/applications/{id:guid}/exam-slots", BookableSlots).WithName("ListBookableExamSlots").ProducesProblems(401, 403, 404);
        family.MapPost("/applications/{id:guid}/book-exam", Book).WithName("BookExam").ProducesProblems(400, 401, 403, 404, 409, 422);
        staff.MapGet("/exam-slots", Slots).WithName("ListExamSlots").ProducesProblems(401, 403);
        staff.MapGet("/exam-slots/{id:guid}/candidates", Candidates).WithName("ListExamCandidates").ProducesProblems(401, 403, 404);
        admin.MapPost("/exam-slots", CreateSlot).WithName("CreateExamSlot").ProducesProblems(400, 401, 403, 404);
        admin.MapPut("/exam-slots/{id:guid}", UpdateSlot).WithName("UpdateExamSlot").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapDelete("/exam-slots/{id:guid}", DeleteSlot).WithName("DeleteExamSlot").ProducesProblems(401, 403, 404, 422);
        academic.MapPut("/applications/{id:guid}/exam-score", Score).WithName("RecordExamScore").ProducesProblems(400, 401, 403, 404, 422);
        academic.MapPost("/applications/{id:guid}/absent", Absent).WithName("MarkExamAbsent").ProducesProblems(401, 403, 404, 422);
        admin.MapPost("/applications/{id:guid}/reopen", Reopen).WithName("ReopenApplication").ProducesProblems(401, 403, 404, 422);
        admin.MapGet("/results", Results).WithName("GetExamResults").ProducesProblems(401, 403, 404);
        admin.MapGet("/results.csv", ResultsCsv).WithName("ExportExamResultsCsv")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv").ProducesProblems(401, 403, 404);
        admin.MapPut("/results/cut-off", CutOff).WithName("SetExamCutOff").ProducesProblems(400, 401, 403, 404);
        admin.MapPost("/results/notify-passed", NotifyPassed).WithName("NotifyStageOnePasses").ProducesProblems(401, 403, 404, 422);

        open.MapGet("", Info).WithName("GetAdmissionsInfo");
        // Sign-ups create accounts: rate-limited per IP like the other public forms.
        open.MapPost("/register", Register).WithName("RegisterApplicant").RequireRateLimiting(RateLimits.PublicForms).ProducesProblems(400, 404, 409, 422, 429);

        family.MapGet("/my-applications", Mine).WithName("ListMyApplications").ProducesProblems(401, 403);
        family.MapPost("/applications", Start).WithName("StartApplication").ProducesProblems(400, 401, 403, 404, 422);
        family.MapPost("/applications/{id:guid}/redeem-code", Redeem).WithName("RedeemFormCode").ProducesProblems(400, 401, 403, 404, 422);
        family.MapPost("/applications/{id:guid}/pay", Pay).WithName("StartFormPayment").ProducesProblems(401, 403, 404, 422);
        open.MapGet("/blank-form.pdf", BlankForm).WithName("GetBlankAdmissionForm")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(404);
        family.MapGet("/applications/{id:guid}", Get).WithName("GetMyApplication").ProducesProblems(401, 403, 404);
        family.MapPut("/applications/{id:guid}/form", SaveForm).WithName("SaveApplicationForm").ProducesProblems(400, 401, 403, 404, 422);
        family.MapPost("/applications/{id:guid}/documents", Upload).WithName("UploadApplicationDocument")
            .DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(PhotoStore.MaxUploadBytes + 64 * 1024))
            .Accepts<IFormFile>("multipart/form-data").ProducesProblems(400, 401, 403, 404, 422);
        family.MapDelete("/applications/{id:guid}/documents/{documentId:guid}", RemoveDocument).WithName("RemoveApplicationDocument").ProducesProblems(401, 403, 404, 422);
        family.MapGet("/applications/{id:guid}/documents/{documentId:guid}/file", Document).WithName("GetMyApplicationDocument").ProducesProblems(401, 403, 404);
        family.MapPost("/applications/{id:guid}/submit", Submit).WithName("SubmitApplication").ProducesProblems(400, 401, 403, 404, 422);
        family.MapGet("/applications/{id:guid}/form.pdf", FormPdf).WithName("GetMyApplicationFormPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);
        finance.MapGet("/applications/{id:guid}/detail", StaffGet).WithName("GetApplication").ProducesProblems(401, 403, 404);
        finance.MapGet("/applications/{id:guid}/documents/{documentId:guid}/download", StaffDocument).WithName("GetApplicationDocument").ProducesProblems(401, 403, 404);
        finance.MapGet("/applications/{id:guid}/form-pdf", StaffFormPdf).WithName("GetApplicationFormPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);
        family.MapGet("/payments/{reference}", Payment).WithName("GetFormPayment").ProducesProblems(401, 403, 404);

        finance.MapGet("/settings", GetSettings).WithName("GetAdmissionSettings").ProducesProblems(400, 401, 403, 404);
        admin.MapPut("/applications/{id:guid}/correction", CorrectForm).WithName("CorrectApplicationForm").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapPut("/settings", SaveSettings).WithName("SaveAdmissionSettings").ProducesProblems(400, 401, 403, 404);
        finance.MapPost("/form-codes", IssueCodes).WithName("IssueFormCodes").ProducesProblems(400, 401, 403, 404);
        finance.MapGet("/form-codes", Codes).WithName("ListFormCodes").ProducesProblems(400, 401, 403);
        finance.MapGet("/applications", List).WithName("ListApplications").ProducesProblems(400, 401, 403);
    }

    private static async Task<Results<Ok<IReadOnlyList<AdmissionsInfo>>, ProblemHttpResult>> Info(IQueryHandler<GetAdmissionsInfoQuery, IReadOnlyList<AdmissionsInfo>> h, CancellationToken ct) =>
        (await h.Handle(new GetAdmissionsInfoQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDto>, ProblemHttpResult>> Register(RegisterApplicantCommand command, ICommandHandler<RegisterApplicantCommand, ApplicationDto> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ApplicationDto>>, ProblemHttpResult>> Mine(IQueryHandler<ListMyApplicationsQuery, IReadOnlyList<ApplicationDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListMyApplicationsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDto>, ProblemHttpResult>> Start(ApplyChild body, ICommandHandler<StartApplicationCommand, ApplicationDto> h, CancellationToken ct) =>
        (await h.Handle(new StartApplicationCommand(body), ct)).ToHttpResult();

    public sealed record CodeRequest(string Code);
    public sealed record BookRequest(Guid SlotId);
    public sealed record SlotRequest(Guid SessionId, DateTimeOffset StartsAt, int DurationMinutes, string Venue, ExamMode Mode, int Capacity, string? Notes);
    public sealed record ScoreRequest(decimal Score);

    private static async Task<Results<Ok<IReadOnlyList<ExamSlotOption>>, ProblemHttpResult>> BookableSlots(Guid id, IQueryHandler<ListBookableSlotsQuery, IReadOnlyList<ExamSlotOption>> h, CancellationToken ct) =>
        (await h.Handle(new ListBookableSlotsQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> Book(Guid id, BookRequest body, ICommandHandler<BookExamCommand, ApplicationDetail> h, CancellationToken ct) =>
        (await h.Handle(new BookExamCommand(id, body.SlotId), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ExamSlotDto>>, ProblemHttpResult>> Slots(Guid sessionId, IQueryHandler<ListExamSlotsQuery, IReadOnlyList<ExamSlotDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListExamSlotsQuery(sessionId), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ExamCandidate>>, ProblemHttpResult>> Candidates(Guid id, IQueryHandler<ListExamCandidatesQuery, IReadOnlyList<ExamCandidate>> h, CancellationToken ct) =>
        (await h.Handle(new ListExamCandidatesQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<ExamSlotDto>, ProblemHttpResult>> CreateSlot(SlotRequest b, ICommandHandler<SaveExamSlotCommand, ExamSlotDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveExamSlotCommand(null, b.SessionId, b.StartsAt, b.DurationMinutes, b.Venue, b.Mode, b.Capacity, b.Notes), ct)).ToHttpResult();

    private static async Task<Results<Ok<ExamSlotDto>, ProblemHttpResult>> UpdateSlot(Guid id, SlotRequest b, ICommandHandler<SaveExamSlotCommand, ExamSlotDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveExamSlotCommand(id, b.SessionId, b.StartsAt, b.DurationMinutes, b.Venue, b.Mode, b.Capacity, b.Notes), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSlot(Guid id, ICommandHandler<DeleteExamSlotCommand> h, CancellationToken ct) =>
        (await h.Handle(new DeleteExamSlotCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<ExamCandidate>, ProblemHttpResult>> Score(Guid id, ScoreRequest body, ICommandHandler<RecordExamScoreCommand, ExamCandidate> h, CancellationToken ct) =>
        (await h.Handle(new RecordExamScoreCommand(id, body.Score), ct)).ToHttpResult();

    private static async Task<Results<Ok<ExamCandidate>, ProblemHttpResult>> Absent(Guid id, ICommandHandler<MarkExamAbsentCommand, ExamCandidate> h, CancellationToken ct) =>
        (await h.Handle(new MarkExamAbsentCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> Reopen(Guid id, ICommandHandler<ReopenApplicationCommand, ApplicationDetail> h, CancellationToken ct) =>
        (await h.Handle(new ReopenApplicationCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<ExamResults>, ProblemHttpResult>> Results([AsParameters] GetExamResultsQuery query, IQueryHandler<GetExamResultsQuery, ExamResults> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    /// <summary>Everyone who took the exam, best first, with Passed/Failed from the current cut-off.</summary>
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ResultsCsv([AsParameters] GetExamResultsQuery query, IQueryHandler<GetExamResultsQuery, ExamResults> h, CancellationToken ct)
    {
        var result = await h.Handle(query, ct);
        if (result.IsFailure) return result.Error!.ToProblem();
        var r = result.Value;
        var csv = new StringBuilder("Reference,Child,Class,Score,Result,Status,First-stage email\r\n");
        foreach (var c in r.Taken)
            csv.Append(string.Join(',', Csv.Cell(c.ReferenceNumber), Csv.Cell(c.ChildName), Csv.Cell(c.ClassLevel), c.Score?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                r.CutOffMark is { } mark ? (c.Score >= mark ? "Passed" : "Failed") : "", c.Status.ToString(), c.StageOneNotifiedAt?.ToString("yyyy-MM-dd") ?? "")).Append("\r\n");
        return TypedResults.File(Csv.Bytes(csv), "text/csv", "Entrance exam results.csv");
    }

    private static async Task<Results<Ok<ExamResults>, ProblemHttpResult>> CutOff(SetCutOffCommand command, ICommandHandler<SetCutOffCommand, ExamResults> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<NotifyResult>, ProblemHttpResult>> NotifyPassed(NotifyStageOnePassesCommand command, ICommandHandler<NotifyStageOnePassesCommand, NotifyResult> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> BlankForm(Guid sessionId, IQueryHandler<GetApplicationFormPdfQuery, PdfFile> h, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await h.Handle(new GetApplicationFormPdfQuery(null, sessionId), ct));

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> Get(Guid id, IQueryHandler<GetApplicationQuery, ApplicationDetail> h, CancellationToken ct) =>
        (await h.Handle(new GetApplicationQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> StaffGet(Guid id, IQueryHandler<GetApplicationQuery, ApplicationDetail> h, CancellationToken ct) =>
        (await h.Handle(new GetApplicationQuery(id, AsStaff: true), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> CorrectForm(Guid id, SaveFormRequest body, ICommandHandler<SaveApplicationFormCommand, ApplicationDetail> h, CancellationToken ct) =>
        (await h.Handle(new SaveApplicationFormCommand(id, body, AsAdmin: true), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> SaveForm(Guid id, SaveFormRequest body, ICommandHandler<SaveApplicationFormCommand, ApplicationDetail> h, CancellationToken ct) =>
        (await h.Handle(new SaveApplicationFormCommand(id, body), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> Upload(
        Guid id, [FromForm] DocumentKind kind, IFormFile? file, ICommandHandler<UploadApplicationDocumentCommand, ApplicationDetail> h, CancellationToken ct)
    {
        if (await PhotoEndpoints.ReadAsync(file, ct) is not { } bytes) return Error.Validation("file", "Choose a photo or PDF to upload.").ToProblem();
        return (await h.Handle(new UploadApplicationDocumentCommand(id, kind, bytes), ct)).ToHttpResult();
    }

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> RemoveDocument(Guid id, Guid documentId, ICommandHandler<RemoveApplicationDocumentCommand, ApplicationDetail> h, CancellationToken ct) =>
        (await h.Handle(new RemoveApplicationDocumentCommand(id, documentId), ct)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> Document(Guid id, Guid documentId, IQueryHandler<GetApplicationDocumentQuery, PhotoContent> h, CancellationToken ct) =>
        FinanceEndpoints.ProofDownload(await h.Handle(new GetApplicationDocumentQuery(id, documentId), ct));

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> StaffDocument(Guid id, Guid documentId, IQueryHandler<GetApplicationDocumentQuery, PhotoContent> h, CancellationToken ct) =>
        FinanceEndpoints.ProofDownload(await h.Handle(new GetApplicationDocumentQuery(id, documentId, AsStaff: true), ct));

    private static async Task<Results<Ok<ApplicationDetail>, ProblemHttpResult>> Submit(Guid id, ICommandHandler<SubmitApplicationCommand, ApplicationDetail> h, CancellationToken ct) =>
        (await h.Handle(new SubmitApplicationCommand(id), ct)).ToHttpResult();

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> FormPdf(Guid id, IQueryHandler<GetApplicationFormPdfQuery, PdfFile> h, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await h.Handle(new GetApplicationFormPdfQuery(id, null), ct));

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> StaffFormPdf(Guid id, IQueryHandler<GetApplicationFormPdfQuery, PdfFile> h, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await h.Handle(new GetApplicationFormPdfQuery(id, null, AsStaff: true), ct));

    private static async Task<Results<Ok<ApplicationDto>, ProblemHttpResult>> Redeem(Guid id, CodeRequest body, ICommandHandler<RedeemFormCodeCommand, ApplicationDto> h, CancellationToken ct) =>
        (await h.Handle(new RedeemFormCodeCommand(id, body.Code), ct)).ToHttpResult();

    private static async Task<Results<Ok<OnlineCheckout>, ProblemHttpResult>> Pay(Guid id, ICommandHandler<StartFormPaymentCommand, OnlineCheckout> h, CancellationToken ct) =>
        (await h.Handle(new StartFormPaymentCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<FormPayments.Outcome>, ProblemHttpResult>> Payment(string reference, IQueryHandler<GetFormPaymentQuery, FormPayments.Outcome> h, CancellationToken ct) =>
        (await h.Handle(new GetFormPaymentQuery(reference), ct)).ToHttpResult();

    private static async Task<Results<Ok<AdmissionSettingsDto>, ProblemHttpResult>> GetSettings([AsParameters] GetAdmissionSettingsQuery query, IQueryHandler<GetAdmissionSettingsQuery, AdmissionSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AdmissionSettingsDto>, ProblemHttpResult>> SaveSettings(SaveAdmissionSettingsCommand command, ICommandHandler<SaveAdmissionSettingsCommand, AdmissionSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<FormCodeDto>>, ProblemHttpResult>> IssueCodes(IssueFormCodesCommand command, ICommandHandler<IssueFormCodesCommand, IReadOnlyList<FormCodeDto>> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<FormCodeDto>>, ProblemHttpResult>> Codes([AsParameters] ListFormCodesQuery query, IQueryHandler<ListFormCodesQuery, PagedResult<FormCodeDto>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<ApplicationDto>>, ProblemHttpResult>> List([AsParameters] ListApplicationsQuery query, IQueryHandler<ListApplicationsQuery, PagedResult<ApplicationDto>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();
}
