using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Application.Finance.CopyFeeItems;
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
/// Fees and invoices (E10.1–E10.2) for admins and bursars. School-wide rules (sibling discount, instalment plans) are
/// admin-only. Payments and receipts come in E10.3.
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
    }

    public sealed record OptInsRequest(IReadOnlyList<Guid> StudentIds);

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
