using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using System.Globalization;
using System.Text;
using Maydel.Application.Finance.AdjustInvoice;
using Maydel.Application.Finance.CancelInvoice;
using Maydel.Application.Finance.CopyFeeItems;
using Maydel.Application.Finance.GetInvoicePdf;
using Maydel.Application.Finance.GetReceiptPdf;
using Maydel.Application.Finance.GetStatement;
using Maydel.Application.Reports;
using Maydel.Application.Finance.ListDebtors;
using Maydel.Application.Finance.RecordPayment;
using Maydel.Application.Finance.ReversePayment;
using Maydel.Application.Finance.DeleteFeeItem;
using Maydel.Application.Finance.GenerateInvoices;
using Maydel.Application.Finance.GetFeeOptIns;
using Maydel.Application.Finance.GetFeeSettings;
using Maydel.Application.Finance.GetInstalmentPlan;
using Maydel.Application.Finance.GetInvoice;
using Maydel.Application.Finance.ListFeeItems;
using Maydel.Application.Finance.ListInvoices;
using Maydel.Application.Finance.SaveFeeItem;
using Maydel.Application.Finance.SaveFeeSettings;
using Maydel.Application.Finance.SaveInstalmentPlan;
using Maydel.Application.Finance.SetFeeOptIns;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Fees, invoices, payments and reports (E10.1–E10.4) for admins and bursars. School-wide rules (sibling discount,
/// instalment plans) and corrections (reversals, cancellations, adjustments) are admin-only.
/// </summary>
public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        var finance = app.MapGroup("/api/finance").WithTags("Finance").RequireAuthorization(Policies.Finance);
        var admin = app.MapGroup("/api/finance").WithTags("Finance").RequireAuthorization(Policies.Admin);

        finance.MapGet("/settings", GetSettings).WithName("GetFeeSettings").ProducesProblems(401, 403);
        admin.MapPut("/settings", SaveSettings).WithName("SaveFeeSettings").ProducesProblems(400, 401, 403);

        finance.MapGet("/fee-items", ListItems).WithName("ListFeeItems").ProducesProblems(400, 401, 403, 404);
        finance.MapPost("/fee-items", CreateItem).WithName("CreateFeeItem").ProducesProblems(400, 401, 403, 404);
        finance.MapPut("/fee-items/{id:guid}", UpdateItem).WithName("UpdateFeeItem").ProducesProblems(400, 401, 403, 404);
        finance.MapDelete("/fee-items/{id:guid}", DeleteItem).WithName("DeleteFeeItem").ProducesProblems(401, 403, 404);
        finance.MapPost("/fee-items/copy", CopyItems).WithName("CopyFeeItems").ProducesProblems(400, 401, 403, 404, 422);
        finance.MapGet("/fee-items/{id:guid}/opt-ins", GetOptIns).WithName("GetFeeOptIns").ProducesProblems(401, 403, 404);
        finance.MapPut("/fee-items/{id:guid}/opt-ins", SetOptIns).WithName("SetFeeOptIns").ProducesProblems(400, 401, 403, 404, 422);

        finance.MapGet("/instalment-plan", GetPlan).WithName("GetInstalmentPlan").ProducesProblems(400, 401, 403, 404);
        admin.MapPut("/instalment-plan", SavePlan).WithName("SaveInstalmentPlan").ProducesProblems(400, 401, 403, 404);

        finance.MapPost("/invoices/generate", Generate).WithName("GenerateInvoices").ProducesProblems(400, 401, 403, 404);
        finance.MapGet("/invoices", ListInvoices).WithName("ListInvoices").ProducesProblems(400, 401, 403);
        finance.MapGet("/invoices/{id:guid}", GetInvoice).WithName("GetInvoice").ProducesProblems(401, 403, 404);
        finance.MapGet("/invoices/{id:guid}/pdf", InvoicePdf).WithName("GetInvoicePdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);
        finance.MapGet("/payments/{id:guid}/receipt.pdf", ReceiptPdf).WithName("GetReceiptPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);
        finance.MapPost("/invoices/{id:guid}/payments", RecordPayment).WithName("RecordPayment").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapPost("/payments/{id:guid}/reverse", ReversePayment).WithName("ReversePayment").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapPost("/invoices/{id:guid}/cancel", CancelInvoice).WithName("CancelInvoice").ProducesProblems(400, 401, 403, 404, 422);
        admin.MapPost("/invoices/{id:guid}/adjustments", AdjustInvoice).WithName("AdjustInvoice").ProducesProblems(400, 401, 403, 404, 422);
        finance.MapGet("/students/{id:guid}/statement", Statement).WithName("GetStudentStatement").ProducesProblems(401, 403, 404);
        finance.MapGet("/debtors", Debtors).WithName("ListDebtors").ProducesProblems(400, 401, 403, 404);
        finance.MapGet("/debtors.csv", DebtorsCsv).WithName("ExportDebtorsCsv")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv").ProducesProblems(400, 401, 403, 404);
    }

    public sealed record OptInsRequest(IReadOnlyList<Guid> StudentIds);
    public sealed record PaymentRequest(decimal Amount, Domain.Finance.PaymentMethod Method, string? Reference, DateOnly? PaidOn);
    public sealed record ReasonRequest(string Reason);
    public sealed record AdjustmentRequest(string Description, decimal Amount);

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> InvoicePdf(Guid id, IQueryHandler<GetInvoicePdfQuery, PdfFile> h, CancellationToken ct) =>
        Pdf(await h.Handle(new GetInvoicePdfQuery(id), ct));

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ReceiptPdf(Guid id, IQueryHandler<GetReceiptPdfQuery, PdfFile> h, CancellationToken ct) =>
        Pdf(await h.Handle(new GetReceiptPdfQuery(id), ct));

    internal static Results<FileContentHttpResult, ProblemHttpResult> Pdf(Result<PdfFile> result) =>
        result.IsSuccess ? TypedResults.File(result.Value.Content, "application/pdf", result.Value.FileName) : result.Error!.ToProblem();

    private static async Task<Results<Ok<ReceiptDto>, ProblemHttpResult>> RecordPayment(Guid id, PaymentRequest body, ICommandHandler<RecordPaymentCommand, ReceiptDto> h, CancellationToken ct) =>
        (await h.Handle(new RecordPaymentCommand(id, body.Amount, body.Method, body.Reference, body.PaidOn), ct)).ToHttpResult();

    private static async Task<Results<Ok<ReceiptDto>, ProblemHttpResult>> ReversePayment(Guid id, ReasonRequest body, ICommandHandler<ReversePaymentCommand, ReceiptDto> h, CancellationToken ct) =>
        (await h.Handle(new ReversePaymentCommand(id, body.Reason), ct)).ToHttpResult();

    private static async Task<Results<Ok<InvoiceDetail>, ProblemHttpResult>> CancelInvoice(Guid id, ReasonRequest body, ICommandHandler<CancelInvoiceCommand, InvoiceDetail> h, CancellationToken ct) =>
        (await h.Handle(new CancelInvoiceCommand(id, body.Reason), ct)).ToHttpResult();

    private static async Task<Results<Ok<InvoiceDetail>, ProblemHttpResult>> AdjustInvoice(Guid id, AdjustmentRequest body, ICommandHandler<AdjustInvoiceCommand, InvoiceDetail> h, CancellationToken ct) =>
        (await h.Handle(new AdjustInvoiceCommand(id, body.Description, body.Amount), ct)).ToHttpResult();

    private static async Task<Results<Ok<StudentStatement>, ProblemHttpResult>> Statement(Guid id, IQueryHandler<GetStatementQuery, StudentStatement> h, CancellationToken ct) =>
        (await h.Handle(new GetStatementQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<DebtorsReport>, ProblemHttpResult>> Debtors([AsParameters] ListDebtorsQuery query, IQueryHandler<ListDebtorsQuery, DebtorsReport> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    /// <summary>Spreadsheet-safe CSV: cells starting with = + - @ are prefixed so Excel never runs them as formulas.</summary>
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> DebtorsCsv([AsParameters] ListDebtorsQuery query, IQueryHandler<ListDebtorsQuery, DebtorsReport> h, CancellationToken ct)
    {
        var result = await h.Handle(query, ct);
        if (result.IsFailure) return result.Error!.ToProblem();
        static string Cell(string? v)
        {
            v ??= "";
            if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
            return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
        }
        static string Money(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
        var csv = new StringBuilder("Admission number,Student,Class,Invoice,Total,Paid,Balance,Due now,Overdue,Next due,Guardian,Guardian phone\r\n");
        foreach (var r in result.Value.Rows)
            csv.Append(string.Join(',', Cell(r.AdmissionNumber), Cell(r.Name), Cell(r.ClassRoom), Cell(r.InvoiceNumber), Money(r.Total), Money(r.Paid),
                Money(r.Balance), Money(r.DueNow), Money(r.Overdue), r.NextDueOn?.ToString("yyyy-MM-dd") ?? "", Cell(r.Guardian), Cell(r.GuardianPhone))).Append("\r\n");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return TypedResults.File(bytes, "text/csv", $"Debtors - {result.Value.Term.Replace('/', '-')}.csv");
    }

    private static async Task<Results<Ok<FeeSettingsDto>, ProblemHttpResult>> GetSettings(IQueryHandler<GetFeeSettingsQuery, FeeSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(new GetFeeSettingsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<FeeSettingsDto>, ProblemHttpResult>> SaveSettings(SaveFeeSettingsCommand command, ICommandHandler<SaveFeeSettingsCommand, FeeSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<FeeItemDto>>, ProblemHttpResult>> ListItems([AsParameters] ListFeeItemsQuery query, IQueryHandler<ListFeeItemsQuery, IReadOnlyList<FeeItemDto>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    public sealed record CreateFeeItemRequest(Guid TermId, string Name, decimal Amount, Guid? ClassLevelId, bool IsMandatory, bool SiblingDiscount);

    private static async Task<Results<Ok<FeeItemDto>, ProblemHttpResult>> CreateItem(CreateFeeItemRequest body, ICommandHandler<SaveFeeItemCommand, FeeItemDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveFeeItemCommand(null, body.TermId, new FeeItemInput(body.Name, body.Amount, body.ClassLevelId, body.IsMandatory, body.SiblingDiscount)), ct)).ToHttpResult();

    private static async Task<Results<Ok<FeeItemDto>, ProblemHttpResult>> UpdateItem(Guid id, CreateFeeItemRequest body, ICommandHandler<SaveFeeItemCommand, FeeItemDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveFeeItemCommand(id, body.TermId, new FeeItemInput(body.Name, body.Amount, body.ClassLevelId, body.IsMandatory, body.SiblingDiscount)), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteItem(Guid id, ICommandHandler<DeleteFeeItemCommand> h, CancellationToken ct) =>
        (await h.Handle(new DeleteFeeItemCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<FeeItemDto>>, ProblemHttpResult>> CopyItems(CopyFeeItemsCommand command, ICommandHandler<CopyFeeItemsCommand, IReadOnlyList<FeeItemDto>> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<OptInStudent>>, ProblemHttpResult>> GetOptIns(Guid id, IQueryHandler<GetFeeOptInsQuery, IReadOnlyList<OptInStudent>> h, CancellationToken ct) =>
        (await h.Handle(new GetFeeOptInsQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<OptInStudent>>, ProblemHttpResult>> SetOptIns(Guid id, OptInsRequest body, ICommandHandler<SetFeeOptInsCommand, IReadOnlyList<OptInStudent>> h, CancellationToken ct) =>
        (await h.Handle(new SetFeeOptInsCommand(id, body.StudentIds ?? []), ct)).ToHttpResult();

    private static async Task<Results<Ok<InstalmentPlanDto>, ProblemHttpResult>> GetPlan([AsParameters] GetInstalmentPlanQuery query, IQueryHandler<GetInstalmentPlanQuery, InstalmentPlanDto> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<InstalmentPlanDto>, ProblemHttpResult>> SavePlan(SaveInstalmentPlanCommand command, ICommandHandler<SaveInstalmentPlanCommand, InstalmentPlanDto> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<GenerateInvoicesResult>, ProblemHttpResult>> Generate(GenerateInvoicesCommand command, ICommandHandler<GenerateInvoicesCommand, GenerateInvoicesResult> h, CancellationToken ct) =>
        (await h.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<InvoiceSummary>>, ProblemHttpResult>> ListInvoices([AsParameters] ListInvoicesQuery query, IQueryHandler<ListInvoicesQuery, PagedResult<InvoiceSummary>> h, CancellationToken ct) =>
        (await h.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<InvoiceDetail>, ProblemHttpResult>> GetInvoice(Guid id, IQueryHandler<GetInvoiceQuery, InvoiceDetail> h, CancellationToken ct) =>
        (await h.Handle(new GetInvoiceQuery(id), ct)).ToHttpResult();
}
