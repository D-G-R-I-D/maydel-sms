using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Application.Store;
using Maydel.Domain.Store;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// The school store (E19): catalogue, stock and office sales for admins and bursars; the parent shop in the portal.
/// Another family's child is a 404.
/// </summary>
public static class StoreEndpoints
{
    public static void MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var store = app.MapGroup("/api/store").WithTags("Store").RequireAuthorization(Policies.Finance);
        store.MapGet("/items", Items).WithName("ListStoreItems").ProducesProblems(401, 403);
        store.MapPost("/items", CreateItem).WithName("CreateStoreItem").ProducesProblems(400, 401, 403);
        store.MapGet("/items/{id:guid}", Item).WithName("GetStoreItem").ProducesProblems(401, 403, 404);
        store.MapPut("/items/{id:guid}", UpdateItem).WithName("UpdateStoreItem").ProducesProblems(400, 401, 403, 404);
        store.MapPut("/items/{id:guid}/image", SetImage).WithName("SetStoreItemImage").Upload();
        store.MapDelete("/items/{id:guid}/image", RemoveImage).WithName("RemoveStoreItemImage").ProducesProblems(401, 403, 404);
        store.MapPost("/variants/{id:guid}/restock", Restock).WithName("RestockVariant").ProducesProblems(400, 401, 403, 404);
        store.MapPost("/variants/{id:guid}/adjust", Adjust).WithName("AdjustVariantStock").ProducesProblems(400, 401, 403, 404, 422);
        store.MapGet("/variants/{id:guid}/movements", Movements).WithName("ListStockMovements").ProducesProblems(401, 403);
        store.MapGet("/low-stock", LowStock).WithName("ListLowStock").ProducesProblems(401, 403);
        store.MapGet("/settings", Settings).WithName("GetStoreSettings").ProducesProblems(401, 403);
        store.MapGet("/orders", Orders).WithName("ListStoreOrders").ProducesProblems(401, 403);
        store.MapPost("/orders", Sell).WithName("CreateOfficeSale").ProducesProblems(400, 401, 403, 404, 409, 422);
        store.MapPost("/orders/{id:guid}/pay", Pay).WithName("PayStoreOrder").ProducesProblems(400, 401, 403, 404, 422);
        store.MapPost("/orders/{id:guid}/ready", Ready).WithName("MarkStoreOrderReady").ProducesProblems(401, 403, 404, 422);
        store.MapPost("/orders/{id:guid}/collected", Collected).WithName("MarkStoreOrderCollected").ProducesProblems(401, 403, 404, 422);
        store.MapPost("/orders/{id:guid}/cancel", Cancel).WithName("CancelStoreOrder").ProducesProblems(401, 403, 404, 422);
        store.MapGet("/orders/{id:guid}/receipt.pdf", Receipt).WithName("GetStoreReceipt").Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);

        var admin = app.MapGroup("/api/store").WithTags("Store").RequireAuthorization(Policies.Admin);
        admin.MapPut("/settings", SaveSettings).WithName("SaveStoreSettings").ProducesProblems(400, 401, 403);

        var shop = app.MapGroup("/api/portal/children/{studentId:guid}/shop").WithTags("Portal").RequireAuthorization(Policies.Portal);
        shop.MapGet("", Shop).WithName("GetChildShop").ProducesProblems(401, 403, 404);
        shop.MapGet("/orders", ShopOrders).WithName("ListChildShopOrders").ProducesProblems(401, 403, 404);
        shop.MapPost("/orders", Place).WithName("PlaceShopOrder").ProducesProblems(400, 401, 403, 404, 409, 422);
        shop.MapPost("/orders/{orderId:guid}/cancel", CancelMine).WithName("CancelShopOrder").ProducesProblems(401, 403, 404, 422);
        shop.MapGet("/orders/{orderId:guid}/receipt.pdf", MyReceipt).WithName("GetShopReceipt").Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404);

        app.MapGet("/api/portal/shop/orders/{orderId:guid}", MyOrder).WithTags("Portal").RequireAuthorization(Policies.Portal)
            .WithName("GetMyShopOrder").ProducesProblems(401, 403, 404);

        var cards = app.MapGroup("/api").WithTags("Store").RequireAuthorization(Policies.Admin);
        cards.MapGet("/students/{id:guid}/id-card.pdf", IdCard).WithName("GetStudentIdCard").Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404, 422);
        cards.MapGet("/academics/classrooms/{id:guid}/id-cards.pdf", ClassIdCards).WithName("GetClassIdCards").Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblems(401, 403, 404, 422);
    }

    private static async Task<Results<Ok<IReadOnlyList<StoreItemDto>>, ProblemHttpResult>> Items(StoreCategory? category, bool? includeInactive, IQueryHandler<ListStoreItemsQuery, IReadOnlyList<StoreItemDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListStoreItemsQuery(category, includeInactive ?? true), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreItemDto>, ProblemHttpResult>> Item(Guid id, IQueryHandler<GetStoreItemQuery, StoreItemDto> h, CancellationToken ct) =>
        (await h.Handle(new GetStoreItemQuery(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreItemDto>, ProblemHttpResult>> CreateItem(StoreItemInput body, ICommandHandler<SaveStoreItemCommand, StoreItemDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveStoreItemCommand(null, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreItemDto>, ProblemHttpResult>> UpdateItem(Guid id, StoreItemInput body, ICommandHandler<SaveStoreItemCommand, StoreItemDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveStoreItemCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreItemDto>, ProblemHttpResult>> SetImage(Guid id, IFormFile? file, ICommandHandler<SetStoreItemImageCommand, StoreItemDto> h, CancellationToken ct) =>
        await PhotoEndpoints.ReadAsync(file, ct) is { } bytes ? (await h.Handle(new SetStoreItemImageCommand(id, bytes), ct)).ToHttpResult() : PhotoErrors.Invalid.ToProblem();
    private static async Task<Results<Ok<StoreItemDto>, ProblemHttpResult>> RemoveImage(Guid id, ICommandHandler<SetStoreItemImageCommand, StoreItemDto> h, CancellationToken ct) =>
        (await h.Handle(new SetStoreItemImageCommand(id, null), ct)).ToHttpResult();
    private static async Task<Results<Ok<VariantDto>, ProblemHttpResult>> Restock(Guid id, RestockInput body, ICommandHandler<RestockCommand, VariantDto> h, CancellationToken ct) =>
        (await h.Handle(new RestockCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<VariantDto>, ProblemHttpResult>> Adjust(Guid id, AdjustStockInput body, ICommandHandler<AdjustStockCommand, VariantDto> h, CancellationToken ct) =>
        (await h.Handle(new AdjustStockCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<MovementDto>>, ProblemHttpResult>> Movements(Guid id, IQueryHandler<ListMovementsQuery, IReadOnlyList<MovementDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListMovementsQuery(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<LowStockRow>>, ProblemHttpResult>> LowStock(IQueryHandler<LowStockQuery, IReadOnlyList<LowStockRow>> h, CancellationToken ct) =>
        (await h.Handle(new LowStockQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreSettingsDto>, ProblemHttpResult>> Settings(IQueryHandler<GetStoreSettingsQuery, StoreSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(new GetStoreSettingsQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreSettingsDto>, ProblemHttpResult>> SaveSettings(StoreSettingsDto body, ICommandHandler<SaveStoreSettingsCommand, StoreSettingsDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveStoreSettingsCommand(body), ct)).ToHttpResult();
    private static async Task<Results<Ok<PagedResult<StoreOrderDto>>, ProblemHttpResult>> Orders([AsParameters] ListStoreOrdersQuery q, IQueryHandler<ListStoreOrdersQuery, PagedResult<StoreOrderDto>> h, CancellationToken ct) =>
        (await h.Handle(q, ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreOrderDto>, ProblemHttpResult>> Sell(OfficeSaleInput body, ICommandHandler<OfficeSaleCommand, StoreOrderDto> h, CancellationToken ct) =>
        (await h.Handle(new OfficeSaleCommand(body), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreOrderDto>, ProblemHttpResult>> Pay(Guid id, PayOrderInput body, ICommandHandler<PayOrderCommand, StoreOrderDto> h, CancellationToken ct) =>
        (await h.Handle(new PayOrderCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreOrderDto>, ProblemHttpResult>> Ready(Guid id, ICommandHandler<OrderReadyCommand, StoreOrderDto> h, CancellationToken ct) =>
        (await h.Handle(new OrderReadyCommand(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreOrderDto>, ProblemHttpResult>> Collected(Guid id, ICommandHandler<OrderCollectedCommand, StoreOrderDto> h, CancellationToken ct) =>
        (await h.Handle(new OrderCollectedCommand(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreOrderDto>, ProblemHttpResult>> Cancel(Guid id, CancelOrderInput body, ICommandHandler<CancelOrderCommand, StoreOrderDto> h, CancellationToken ct) =>
        (await h.Handle(new CancelOrderCommand(id, body.Reason), ct)).ToHttpResult();

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> Receipt(Guid id, IQueryHandler<GetStoreReceiptQuery, Maydel.Application.Reports.PdfFile> h, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await h.Handle(new GetStoreReceiptQuery(id), ct));
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> MyReceipt(Guid studentId, Guid orderId, IQueryHandler<GetStoreReceiptQuery, Maydel.Application.Reports.PdfFile> h, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await h.Handle(new GetStoreReceiptQuery(orderId, studentId), ct));
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> IdCard(Guid id, IQueryHandler<GetIdCardQuery, Maydel.Application.Reports.PdfFile> h, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await h.Handle(new GetIdCardQuery(id), ct));
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ClassIdCards(Guid id, IQueryHandler<GetClassIdCardsQuery, Maydel.Application.Reports.PdfFile> h, CancellationToken ct) =>
        FinanceEndpoints.Pdf(await h.Handle(new GetClassIdCardsQuery(id), ct));

    private static async Task<Results<Ok<ShopCatalogue>, ProblemHttpResult>> Shop(Guid studentId, IQueryHandler<GetShopQuery, ShopCatalogue> h, CancellationToken ct) =>
        (await h.Handle(new GetShopQuery(studentId), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<StoreOrderDto>>, ProblemHttpResult>> ShopOrders(Guid studentId, IQueryHandler<ListShopOrdersQuery, IReadOnlyList<StoreOrderDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListShopOrdersQuery(studentId), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreOrderDto>, ProblemHttpResult>> MyOrder(Guid orderId, IQueryHandler<GetMyShopOrderQuery, StoreOrderDto> h, CancellationToken ct) =>
        (await h.Handle(new GetMyShopOrderQuery(orderId), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreOrderDto>, ProblemHttpResult>> Place(Guid studentId, ShopOrderInput body, ICommandHandler<PlaceShopOrderCommand, StoreOrderDto> h, CancellationToken ct) =>
        (await h.Handle(new PlaceShopOrderCommand(studentId, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<StoreOrderDto>, ProblemHttpResult>> CancelMine(Guid studentId, Guid orderId, ICommandHandler<CancelShopOrderCommand, StoreOrderDto> h, CancellationToken ct) =>
        (await h.Handle(new CancelShopOrderCommand(studentId, orderId), ct)).ToHttpResult();
}
