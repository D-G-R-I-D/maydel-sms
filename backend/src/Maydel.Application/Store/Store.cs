using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Notifications;
using Maydel.Application.Photos;
using Maydel.Application.Website;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.Store;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Store;

public sealed record VariantInput(Guid? Id, string Label, decimal Price, int LowStockAt = 5, bool IsActive = true);
public sealed record StoreItemInput(string Name, StoreCategory Category, string? Description, StoreSections Sections, bool IsActive, IReadOnlyList<VariantInput> Variants);
public sealed record VariantDto(Guid Id, string Label, decimal Price, int Stock, int LowStockAt, bool IsLow, bool IsActive);
public sealed record StoreItemDto(Guid Id, string Name, StoreCategory Category, string? Description, StoreSections Sections, bool IsActive, string? ImageUrl,
    IReadOnlyList<VariantDto> Variants);

public sealed record RestockInput(int Quantity, decimal? UnitCost, string? Supplier, string? Note);
public sealed record AdjustStockInput(int Change, string Reason);
public sealed record MovementDto(Guid Id, MovementKind Kind, int Change, string? Note, decimal? UnitCost, string? Supplier, string? OrderNumber, DateTimeOffset CreatedAt);
public sealed record LowStockRow(Guid ItemId, string Item, Guid VariantId, string Variant, int Stock, int LowStockAt);

public sealed record OrderLineInput(Guid VariantId, int Quantity);
public sealed record OfficeSaleInput(Guid StudentId, IReadOnlyList<OrderLineInput> Lines, StorePayMethod Payment, string? Reference, bool HandedOver = true);
public sealed record ShopOrderInput(IReadOnlyList<OrderLineInput> Lines, StorePayMethod Payment);
public sealed record PayOrderInput(StorePayMethod Method, string? Reference);
public sealed record CancelOrderInput(string? Reason);

public sealed record OrderLineDto(Guid VariantId, string Item, string Variant, decimal UnitPrice, int Quantity, decimal LineTotal);
public sealed record StoreOrderDto(Guid Id, string Number, Guid StudentId, string Student, string AdmissionNumber, bool FromPortal, string PlacedBy,
    OrderStatus Status, OrderPayment Payment, StorePayMethod? PaidWith, string? PaymentReference, decimal Total, IReadOnlyList<OrderLineDto> Lines,
    DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, DateTimeOffset? ReadyAt, DateTimeOffset? CollectedAt, DateTimeOffset? CancelledAt,
    string? CancelReason, DateTimeOffset? HoldUntil, string? InvoiceNumber);

public sealed record StoreSettingsDto(bool ParentsCanChargeToFees, int HoldHours);

/// <summary>The parent shop for one child: only items for the child's section, with what's in stock.</summary>
public sealed record ShopCatalogue(Guid StudentId, string Student, bool CanChargeToFees, int HoldHours, IReadOnlyList<StoreItemDto> Items);

public static class StoreErrors
{
    public static readonly Error ItemNotFound = Error.NotFound("store.item_not_found", "That item doesn't exist.");
    public static readonly Error VariantNotFound = Error.NotFound("store.variant_not_found", "That option doesn't exist.");
    public static readonly Error OrderNotFound = Error.NotFound("store.order_not_found", "That order doesn't exist.");
    public static readonly Error EmptyOrder = Error.Validation("lines", "Add at least one item.");
    public static readonly Error NoInvoice = Error.Rule("store.no_invoice", "There's no fees invoice for this student this term to add it to. Take payment instead.");
    public static Error OutOfStock(string what, int left) => Error.Rule("store.out_of_stock", left == 0 ? $"{what} is out of stock." : $"Only {left} of {what} left.");
}

internal static class StoreQueries
{
    public static StoreItemDto Dto(this StoreItem i, bool activeVariantsOnly = false) => new(i.Id, i.Name, i.Category, i.Description, i.Sections, i.IsActive,
        WebsiteUrls.Image("store", i.Id, i.ImagePath),
        [.. i.Variants.Where(v => !activeVariantsOnly || v.IsActive).OrderBy(v => v.Order)
            .Select(v => new VariantDto(v.Id, v.Label, v.Price, v.Stock, v.LowStockAt, v.Stock <= v.LowStockAt, v.IsActive))]);

    public static async Task<StoreSettings> StoreSettingsAsync(this IAppDbContext db, CancellationToken ct) =>
        await db.StoreSettings.OrderBy(s => s.CreatedAt).FirstOrDefaultAsync(ct) ?? new StoreSettings();

    public static async Task<IReadOnlyList<StoreOrderDto>> DtosAsync(this IAppDbContext db, IReadOnlyList<StoreOrder> orders, CancellationToken ct)
    {
        var studentIds = orders.Select(o => o.StudentId).Distinct().ToList();
        var students = await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { Name = s.FirstName + " " + s.LastName, s.AdmissionNumber }, ct);
        var invoiceIds = orders.Where(o => o.InvoiceId != null).Select(o => o.InvoiceId!.Value).ToList();
        var invoices = await db.Invoices.AsNoTracking().Where(i => invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Number, ct);
        return [.. orders.Select(o => new StoreOrderDto(o.Id, o.Number, o.StudentId, students.GetValueOrDefault(o.StudentId)?.Name ?? "",
            students.GetValueOrDefault(o.StudentId)?.AdmissionNumber ?? "", o.FromPortal, o.PlacedByName, o.Status, o.Payment, o.PaidWith, o.PaymentReference, o.Total,
            [.. o.Lines.Select(l => new OrderLineDto(l.VariantId, l.ItemName, l.VariantLabel, l.UnitPrice, l.Quantity, l.LineTotal))],
            o.CreatedAt, o.PaidAt, o.ReadyAt, o.CollectedAt, o.CancelledAt, o.CancelReason, o.HoldUntil,
            o.InvoiceId is { } inv ? invoices.GetValueOrDefault(inv) : null))];
    }
}

/// <summary>Builds and books orders: prices and stock checked, stock taken, and (for fees) the invoice line added.</summary>
internal sealed class StoreOrders(IAppDbContext db, ICurrentUser me, ISchoolClock clock)
{
    public async Task<string> NameAsync(CancellationToken ct)
    {
        var uid = me.UserId!.Value;
        return await db.Staff.AsNoTracking().Where(s => s.UserId == uid).Select(s => s.FullName).FirstOrDefaultAsync(ct)
            ?? await db.Guardians.AsNoTracking().Where(g => g.UserId == uid).Select(g => g.FullName).FirstOrDefaultAsync(ct)
            ?? me.UserName ?? "Someone";
    }

    /// <summary>Takes the lines' stock and returns the order (not yet added). Only items on sale, for the student's section.</summary>
    public async Task<Result<StoreOrder>> BuildAsync(Domain.People.Student student, IReadOnlyList<OrderLineInput>? lines, bool fromPortal, MovementKind take,
        CancellationToken ct)
    {
        var wanted = (lines ?? []).Where(l => l.Quantity != 0).GroupBy(l => l.VariantId).Select(g => (Id: g.Key, Qty: g.Sum(l => l.Quantity))).ToList();
        if (wanted.Count == 0) return StoreErrors.EmptyOrder;
        if (wanted.Any(w => w.Qty is < 1 or > 50)) return Error.Validation("lines", "Quantities must be between 1 and 50.");
        var ids = wanted.Select(w => w.Id).ToList();
        var variants = await db.StoreVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var itemIds = variants.Values.Select(v => v.ItemId).Distinct().ToList();
        var items = await db.StoreItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var section = student.ClassRoomId is { } room
            ? await db.ClassRooms.AsNoTracking().Where(r => r.Id == room).Select(r => (Domain.Academics.SchoolSection?)r.ClassLevel.Section).FirstOrDefaultAsync(ct)
            : null;

        var order = new StoreOrder { Number = "", StudentId = student.Id, PlacedByUserId = me.UserId!.Value, PlacedByName = await NameAsync(ct), FromPortal = fromPortal };
        foreach (var (id, qty) in wanted)
        {
            if (!variants.TryGetValue(id, out var v) || !v.IsActive || !items.TryGetValue(v.ItemId, out var item) || !item.IsActive) return StoreErrors.VariantNotFound;
            if (fromPortal && !item.IsFor(section)) return StoreErrors.VariantNotFound;
            if (v.Stock < qty) return StoreErrors.OutOfStock($"{item.Name} ({v.Label})", Math.Max(0, v.Stock));
            v.Stock -= qty;
            db.StockMovements.Add(new StockMovement { VariantId = v.Id, Change = -qty, Kind = take, OrderId = order.Id, ByUserId = me.UserId });
            order.Lines.Add(new StoreOrderLine { VariantId = v.Id, ItemName = item.Name, VariantLabel = v.Label, UnitPrice = v.Price, Quantity = qty });
        }
        order.Total = order.Lines.Sum(l => l.LineTotal);
        order.Number = await NextNumberAsync(ct);
        return order;
    }

    /// <summary>Adds the order to the student's fees invoice for the current term.</summary>
    public async Task<Error?> ChargeToFeesAsync(StoreOrder order, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Payments)
            .Where(i => i.StudentId == order.StudentId && !i.IsCancelled && i.Term.IsCurrent).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(ct);
        if (invoice is null) return StoreErrors.NoInvoice;
        var what = string.Join(", ", order.Lines.Select(l => $"{l.ItemName} ({l.VariantLabel}) ×{l.Quantity}"));
        var description = $"Store order {order.Number}: {what}";
        if (invoice.Adjust(description.Length > 250 ? description[..249] + "…" : description, order.Total) is { } invalid) return invalid.ToError();
        (order.Payment, order.PaidWith, order.InvoiceId, order.PaidAt, order.HoldUntil) = (OrderPayment.ChargedToFees, StorePayMethod.Fees, invoice.Id, clock.Now.ToUniversalTime(), null);
        return null;
    }

    public void Release(StoreOrder order, Dictionary<Guid, StoreVariant> variants, string why)
    {
        foreach (var line in order.Lines)
            if (variants.TryGetValue(line.VariantId, out var v))
            {
                v.Stock += line.Quantity;
                db.StockMovements.Add(new StockMovement { VariantId = v.Id, Change = line.Quantity, Kind = MovementKind.Release, OrderId = order.Id, Note = why, ByUserId = me.UserId });
            }
    }

    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var prefix = $"ORD-{clock.Today.Year}-";
        var last = await db.StoreOrders.Where(o => o.Number.StartsWith(prefix)).OrderByDescending(o => o.Number).Select(o => o.Number).FirstOrDefaultAsync(ct);
        var next = last is not null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{next:00000}";
    }
}

// ---------- Catalogue and stock (admin, bursar) ----------

public sealed record ListStoreItemsQuery(StoreCategory? Category = null, bool IncludeInactive = true) : IQuery<IReadOnlyList<StoreItemDto>>;
public sealed record SaveStoreItemCommand(Guid? Id, StoreItemInput Input) : ICommand<StoreItemDto>;
public sealed record SetStoreItemImageCommand(Guid Id, byte[]? File) : ICommand<StoreItemDto>;
public sealed record RestockCommand(Guid VariantId, RestockInput Input) : ICommand<VariantDto>;
public sealed record AdjustStockCommand(Guid VariantId, AdjustStockInput Input) : ICommand<VariantDto>;
public sealed record ListMovementsQuery(Guid VariantId) : IQuery<IReadOnlyList<MovementDto>>;
public sealed record LowStockQuery : IQuery<IReadOnlyList<LowStockRow>>;
public sealed record GetStoreSettingsQuery : IQuery<StoreSettingsDto>;
public sealed record SaveStoreSettingsCommand(StoreSettingsDto Settings) : ICommand<StoreSettingsDto>;

internal sealed class ListStoreItemsHandler(IAppDbContext db) : IQueryHandler<ListStoreItemsQuery, IReadOnlyList<StoreItemDto>>
{
    public async Task<Result<IReadOnlyList<StoreItemDto>>> Handle(ListStoreItemsQuery q, CancellationToken ct)
    {
        var items = db.StoreItems.AsNoTracking().Include(i => i.Variants).AsQueryable();
        if (q.Category is { } c) items = items.Where(i => i.Category == c);
        if (!q.IncludeInactive) items = items.Where(i => i.IsActive);
        return (await items.OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync(ct)).Select(i => i.Dto()).ToList();
    }
}

internal sealed class SaveStoreItemHandler(IAppDbContext db) : ICommandHandler<SaveStoreItemCommand, StoreItemDto>
{
    public async Task<Result<StoreItemDto>> Handle(SaveStoreItemCommand c, CancellationToken ct)
    {
        StoreItem item;
        if (c.Id is { } id)
        {
            if (await db.StoreItems.Include(i => i.Variants).FirstOrDefaultAsync(i => i.Id == id, ct) is not { } found) return StoreErrors.ItemNotFound;
            item = found;
        }
        else db.StoreItems.Add(item = new StoreItem { Name = "" });
        var i = c.Input;
        if (!Enum.IsDefined(i.Category)) return Error.Validation("category", "Choose a category.");
        (item.Name, item.Category, item.Description, item.Sections, item.IsActive) =
            (i.Name ?? "", i.Category, string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(), i.Sections, i.IsActive);

        // Options: update by id, add new ones; ones left out are retired (kept for past sales).
        var order = 0;
        var kept = new HashSet<Guid>();
        foreach (var v in i.Variants ?? [])
        {
            StoreVariant variant;
            if (v.Id is { } vid)
            {
                if (item.Variants.FirstOrDefault(x => x.Id == vid) is not { } existing) return StoreErrors.VariantNotFound;
                variant = existing;
            }
            else item.Variants.Add(variant = new StoreVariant { Label = "" });
            (variant.Label, variant.Price, variant.LowStockAt, variant.IsActive, variant.Order) = ((v.Label ?? "").Trim(), v.Price, v.LowStockAt, v.IsActive, order++);
            kept.Add(variant.Id);
        }
        foreach (var gone in item.Variants.Where(v => !kept.Contains(v.Id))) gone.IsActive = false;
        if (item.Check() is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return item.Dto();
    }
}

internal sealed class SetStoreItemImageHandler(IAppDbContext db, PhotoStore photos) : ICommandHandler<SetStoreItemImageCommand, StoreItemDto>
{
    public async Task<Result<StoreItemDto>> Handle(SetStoreItemImageCommand c, CancellationToken ct)
    {
        if (await db.StoreItems.Include(i => i.Variants).FirstOrDefaultAsync(i => i.Id == c.Id, ct) is not { } item) return StoreErrors.ItemNotFound;
        var old = item.ImagePath;
        if (c.File is { } file)
        {
            var saved = await photos.SaveAsync(file, "store", transparent: false, 1200, ct);
            if (saved.IsFailure) return saved.Error!;
            item.ImagePath = saved.Value.Key;
        }
        else item.ImagePath = null;
        await db.SaveChangesAsync(ct);
        if (old is not null && old != item.ImagePath) await photos.DeleteQuietlyAsync(old, ct);
        return item.Dto();
    }
}

internal sealed class RestockHandler(IAppDbContext db, ICurrentUser me) : ICommandHandler<RestockCommand, VariantDto>
{
    public async Task<Result<VariantDto>> Handle(RestockCommand c, CancellationToken ct)
    {
        if (await db.StoreVariants.FirstOrDefaultAsync(v => v.Id == c.VariantId, ct) is not { } v) return StoreErrors.VariantNotFound;
        var i = c.Input;
        if (i.Quantity is < 1 or > 10000) return Error.Validation("quantity", "Restock between 1 and 10,000.");
        if (i.UnitCost is < 0) return Error.Validation("unitCost", "The cost can't be negative.");
        v.Stock += i.Quantity;
        db.StockMovements.Add(new StockMovement
        {
            VariantId = v.Id, Change = i.Quantity, Kind = MovementKind.Restock, UnitCost = i.UnitCost, ByUserId = me.UserId,
            Supplier = string.IsNullOrWhiteSpace(i.Supplier) ? null : i.Supplier.Trim(), Note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim(),
        });
        await db.SaveChangesAsync(ct);
        return new VariantDto(v.Id, v.Label, v.Price, v.Stock, v.LowStockAt, v.Stock <= v.LowStockAt, v.IsActive);
    }
}

internal sealed class AdjustStockHandler(IAppDbContext db, ICurrentUser me) : ICommandHandler<AdjustStockCommand, VariantDto>
{
    public async Task<Result<VariantDto>> Handle(AdjustStockCommand c, CancellationToken ct)
    {
        if (await db.StoreVariants.FirstOrDefaultAsync(v => v.Id == c.VariantId, ct) is not { } v) return StoreErrors.VariantNotFound;
        if (c.Input.Change == 0) return Error.Validation("change", "Enter how many to add (+) or remove (−).");
        if (string.IsNullOrWhiteSpace(c.Input.Reason)) return Error.Validation("reason", "Say why the stock is changing (count, damaged, lost…).");
        if (v.Stock + c.Input.Change < 0) return Error.Rule("store.negative_stock", $"There are only {v.Stock} in stock.");
        v.Stock += c.Input.Change;
        db.StockMovements.Add(new StockMovement { VariantId = v.Id, Change = c.Input.Change, Kind = MovementKind.Adjustment, Note = c.Input.Reason.Trim(), ByUserId = me.UserId });
        await db.SaveChangesAsync(ct);
        return new VariantDto(v.Id, v.Label, v.Price, v.Stock, v.LowStockAt, v.Stock <= v.LowStockAt, v.IsActive);
    }
}

internal sealed class ListMovementsHandler(IAppDbContext db) : IQueryHandler<ListMovementsQuery, IReadOnlyList<MovementDto>>
{
    public async Task<Result<IReadOnlyList<MovementDto>>> Handle(ListMovementsQuery q, CancellationToken ct) =>
        await db.StockMovements.AsNoTracking().Where(m => m.VariantId == q.VariantId).OrderByDescending(m => m.CreatedAt).Take(200)
            .Select(m => new MovementDto(m.Id, m.Kind, m.Change, m.Note, m.UnitCost, m.Supplier,
                db.StoreOrders.Where(o => o.Id == m.OrderId).Select(o => o.Number).FirstOrDefault(), m.CreatedAt)).ToListAsync(ct);
}

internal sealed class LowStockHandler(IAppDbContext db) : IQueryHandler<LowStockQuery, IReadOnlyList<LowStockRow>>
{
    public async Task<Result<IReadOnlyList<LowStockRow>>> Handle(LowStockQuery q, CancellationToken ct) =>
        await db.StoreVariants.AsNoTracking().Where(v => v.IsActive && v.Stock <= v.LowStockAt)
            .Join(db.StoreItems.Where(i => i.IsActive), v => v.ItemId, i => i.Id, (v, i) => new LowStockRow(i.Id, i.Name, v.Id, v.Label, v.Stock, v.LowStockAt))
            .OrderBy(r => r.Stock).ThenBy(r => r.Item).ToListAsync(ct);
}

internal sealed class GetStoreSettingsHandler(IAppDbContext db) : IQueryHandler<GetStoreSettingsQuery, StoreSettingsDto>
{
    public async Task<Result<StoreSettingsDto>> Handle(GetStoreSettingsQuery q, CancellationToken ct)
    {
        var s = await db.StoreSettingsAsync(ct);
        return new StoreSettingsDto(s.ParentsCanChargeToFees, s.HoldHours);
    }
}

internal sealed class SaveStoreSettingsHandler(IAppDbContext db) : ICommandHandler<SaveStoreSettingsCommand, StoreSettingsDto>
{
    public async Task<Result<StoreSettingsDto>> Handle(SaveStoreSettingsCommand c, CancellationToken ct)
    {
        if (c.Settings.HoldHours is < 1 or > 336) return Error.Validation("holdHours", "Hold unpaid orders for 1 hour to 14 days.");
        var s = await db.StoreSettings.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (s is null) db.StoreSettings.Add(s = new StoreSettings());
        (s.ParentsCanChargeToFees, s.HoldHours) = (c.Settings.ParentsCanChargeToFees, c.Settings.HoldHours);
        await db.SaveChangesAsync(ct);
        return new StoreSettingsDto(s.ParentsCanChargeToFees, s.HoldHours);
    }
}

// ---------- Orders (office) ----------

public sealed record ListStoreOrdersQuery(OrderStatus? Status = null, Guid? StudentId = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<StoreOrderDto>>;
public sealed record OfficeSaleCommand(OfficeSaleInput Input) : ICommand<StoreOrderDto>;
public sealed record PayOrderCommand(Guid Id, PayOrderInput Input) : ICommand<StoreOrderDto>;
public sealed record OrderReadyCommand(Guid Id) : ICommand<StoreOrderDto>;
public sealed record OrderCollectedCommand(Guid Id) : ICommand<StoreOrderDto>;
public sealed record CancelOrderCommand(Guid Id, string? Reason) : ICommand<StoreOrderDto>;

internal sealed class ListStoreOrdersHandler(IAppDbContext db) : IQueryHandler<ListStoreOrdersQuery, PagedResult<StoreOrderDto>>
{
    public async Task<Result<PagedResult<StoreOrderDto>>> Handle(ListStoreOrdersQuery q, CancellationToken ct)
    {
        var orders = db.StoreOrders.AsNoTracking();
        if (q.Status is { } s) orders = orders.Where(o => o.Status == s);
        if (q.StudentId is { } sid) orders = orders.Where(o => o.StudentId == sid);
        var page = await orders.OrderByDescending(o => o.CreatedAt).ToPagedAsync(q.Page, q.PageSize, ct);
        return new PagedResult<StoreOrderDto>([.. await db.DtosAsync(page.Items, ct)], page.Page, page.PageSize, page.TotalCount);
    }
}

internal sealed class OfficeSaleHandler(IAppDbContext db, StoreOrders orders, ISchoolClock clock) : ICommandHandler<OfficeSaleCommand, StoreOrderDto>
{
    public async Task<Result<StoreOrderDto>> Handle(OfficeSaleCommand c, CancellationToken ct)
    {
        var i = c.Input;
        if (await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == i.StudentId, ct) is not { } student) return Error.Validation("studentId", "Choose a student.");
        if (i.Payment is StorePayMethod.AtOffice) return Error.Validation("payment", "Choose cash, bank transfer, POS or fees.");
        var built = await orders.BuildAsync(student, i.Lines, fromPortal: false, MovementKind.Sale, ct);
        if (built.IsFailure) return built.Error!;
        var order = built.Value;
        var now = clock.Now.ToUniversalTime();
        if (i.Payment == StorePayMethod.Fees)
        {
            if (await orders.ChargeToFeesAsync(order, ct) is { } error) return error;
        }
        else if (order.MarkPaid(i.Payment, i.Reference, now) is { } invalid) return invalid.ToError();
        if (i.HandedOver) order.MarkCollected(now);
        db.StoreOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([order], ct))[0];
    }
}

internal sealed class PayOrderHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<PayOrderCommand, StoreOrderDto>
{
    public async Task<Result<StoreOrderDto>> Handle(PayOrderCommand c, CancellationToken ct)
    {
        if (await db.StoreOrders.FirstOrDefaultAsync(o => o.Id == c.Id, ct) is not { } order) return StoreErrors.OrderNotFound;
        if (order.MarkPaid(c.Input.Method, c.Input.Reference, clock.Now.ToUniversalTime()) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([order], ct))[0];
    }
}

internal sealed class OrderReadyHandler(IAppDbContext db, ISchoolClock clock, Notifier notifier) : ICommandHandler<OrderReadyCommand, StoreOrderDto>
{
    public async Task<Result<StoreOrderDto>> Handle(OrderReadyCommand c, CancellationToken ct)
    {
        if (await db.StoreOrders.FirstOrDefaultAsync(o => o.Id == c.Id, ct) is not { } order) return StoreErrors.OrderNotFound;
        if (order.MarkReady(clock.Now.ToUniversalTime()) is { } invalid) return invalid.ToError();
        var child = await db.Students.AsNoTracking().Where(s => s.Id == order.StudentId).Select(s => s.FirstName).FirstAsync(ct);
        var pay = order.Payment == OrderPayment.Unpaid ? $" Please pay ₦{order.Total:N2} at the office when you collect." : "";
        await notifier.GuardiansAsync(order.StudentId, NotificationKind.StoreOrderReady, $"{child}'s order is ready to collect",
            $"Order {order.Number} is waiting at the school office.{pay}", $"/portal/shop/orders/{order.Id}", $"store:{order.Id}:ready", ct);
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([order], ct))[0];
    }
}

internal sealed class OrderCollectedHandler(IAppDbContext db, ISchoolClock clock) : ICommandHandler<OrderCollectedCommand, StoreOrderDto>
{
    public async Task<Result<StoreOrderDto>> Handle(OrderCollectedCommand c, CancellationToken ct)
    {
        if (await db.StoreOrders.FirstOrDefaultAsync(o => o.Id == c.Id, ct) is not { } order) return StoreErrors.OrderNotFound;
        if (order.MarkCollected(clock.Now.ToUniversalTime()) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([order], ct))[0];
    }
}

internal sealed class CancelOrderHandler(IAppDbContext db, StoreOrders orders, ISchoolClock clock) : ICommandHandler<CancelOrderCommand, StoreOrderDto>
{
    public async Task<Result<StoreOrderDto>> Handle(CancelOrderCommand c, CancellationToken ct)
    {
        if (await db.StoreOrders.FirstOrDefaultAsync(o => o.Id == c.Id, ct) is not { } order) return StoreErrors.OrderNotFound;
        if (order.Cancel(c.Reason, clock.Now.ToUniversalTime()) is { } invalid) return invalid.ToError();
        var ids = order.Lines.Select(l => l.VariantId).ToList();
        orders.Release(order, await db.StoreVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct), "Order cancelled");
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([order], ct))[0];
    }
}

/// <summary>Releases unpaid parent orders whose hold has run out (stock goes back). Background worker.</summary>
public sealed record ReleaseExpiredOrdersCommand(DateTimeOffset? Now = null) : ICommand<int>;

internal sealed class ReleaseExpiredOrdersHandler(IAppDbContext db, StoreOrders orders, ISchoolClock clock) : ICommandHandler<ReleaseExpiredOrdersCommand, int>
{
    public async Task<Result<int>> Handle(ReleaseExpiredOrdersCommand c, CancellationToken ct)
    {
        var now = (c.Now ?? clock.Now).ToUniversalTime();
        var expired = await db.StoreOrders.Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Collected
            && o.Payment == OrderPayment.Unpaid && o.HoldUntil != null && o.HoldUntil < now).ToListAsync(ct);
        if (expired.Count == 0) return 0;
        var ids = expired.SelectMany(o => o.Lines.Select(l => l.VariantId)).Distinct().ToList();
        var variants = await db.StoreVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        foreach (var order in expired)
        {
            order.Cancel("Not paid in time", now);
            orders.Release(order, variants, "Hold expired");
        }
        await db.SaveChangesAsync(ct);
        return expired.Count;
    }
}

// ---------- Parent shop (portal) ----------

public sealed record GetShopQuery(Guid StudentId) : IQuery<ShopCatalogue>;
public sealed record PlaceShopOrderCommand(Guid StudentId, ShopOrderInput Input) : ICommand<StoreOrderDto>;
public sealed record ListShopOrdersQuery(Guid StudentId) : IQuery<IReadOnlyList<StoreOrderDto>>;
public sealed record CancelShopOrderCommand(Guid StudentId, Guid OrderId) : ICommand<StoreOrderDto>;

internal sealed class GetShopHandler(IAppDbContext db, AccessRules access) : IQueryHandler<GetShopQuery, ShopCatalogue>
{
    public async Task<Result<ShopCatalogue>> Handle(GetShopQuery q, CancellationToken ct)
    {
        if (await access.PortalStudents().AsNoTracking().FirstOrDefaultAsync(s => s.Id == q.StudentId, ct) is not { } student)
            return Error.NotFound("portal.student_not_found", "That student isn't linked to your account.");
        var section = student.ClassRoomId is { } room
            ? await db.ClassRooms.AsNoTracking().Where(r => r.Id == room).Select(r => (Domain.Academics.SchoolSection?)r.ClassLevel.Section).FirstOrDefaultAsync(ct)
            : null;
        var items = await db.StoreItems.AsNoTracking().Include(i => i.Variants).Where(i => i.IsActive).OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync(ct);
        var settings = await db.StoreSettingsAsync(ct);
        return new ShopCatalogue(student.Id, $"{student.FirstName} {student.LastName}", settings.ParentsCanChargeToFees, settings.HoldHours,
            [.. items.Where(i => i.IsFor(section)).Select(i => i.Dto(activeVariantsOnly: true))]);
    }
}

internal sealed class PlaceShopOrderHandler(IAppDbContext db, AccessRules access, ICurrentUser me, StoreOrders orders, ISchoolClock clock)
    : ICommandHandler<PlaceShopOrderCommand, StoreOrderDto>
{
    public async Task<Result<StoreOrderDto>> Handle(PlaceShopOrderCommand c, CancellationToken ct)
    {
        if (!me.IsInRole(Roles.Parent)) return Error.Forbidden("shop.parents_only", "Only a parent or guardian can order from the shop.");
        if (await access.PortalStudents().AsNoTracking().FirstOrDefaultAsync(s => s.Id == c.StudentId, ct) is not { } student)
            return Error.NotFound("portal.student_not_found", "That student isn't linked to your account.");
        var settings = await db.StoreSettingsAsync(ct);
        if (c.Input.Payment is not (StorePayMethod.AtOffice or StorePayMethod.Fees)) return Error.Validation("payment", "Choose pay at the office or add to fees.");
        if (c.Input.Payment == StorePayMethod.Fees && !settings.ParentsCanChargeToFees) return Error.Rule("shop.no_fees", "The school isn't taking shop orders on the fees account. Choose pay at the office.");

        var built = await orders.BuildAsync(student, c.Input.Lines, fromPortal: true, MovementKind.Reserve, ct);
        if (built.IsFailure) return built.Error!;
        var order = built.Value;
        if (c.Input.Payment == StorePayMethod.Fees)
        {
            if (await orders.ChargeToFeesAsync(order, ct) is { } error) return error;
        }
        else order.HoldUntil = clock.Now.ToUniversalTime().AddHours(settings.HoldHours);
        db.StoreOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([order], ct))[0];
    }
}

internal sealed class ListShopOrdersHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListShopOrdersQuery, IReadOnlyList<StoreOrderDto>>
{
    public async Task<Result<IReadOnlyList<StoreOrderDto>>> Handle(ListShopOrdersQuery q, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == q.StudentId, ct)) return Error.NotFound("portal.student_not_found", "That student isn't linked to your account.");
        var list = await db.StoreOrders.AsNoTracking().Where(o => o.StudentId == q.StudentId).OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync(ct);
        return Result<IReadOnlyList<StoreOrderDto>>.Success(await db.DtosAsync(list, ct));
    }
}

internal sealed class CancelShopOrderHandler(IAppDbContext db, AccessRules access, ICurrentUser me, StoreOrders orders, ISchoolClock clock)
    : ICommandHandler<CancelShopOrderCommand, StoreOrderDto>
{
    public async Task<Result<StoreOrderDto>> Handle(CancelShopOrderCommand c, CancellationToken ct)
    {
        if (!me.IsInRole(Roles.Parent) || !await access.PortalStudents().AnyAsync(s => s.Id == c.StudentId, ct)) return StoreErrors.OrderNotFound;
        if (await db.StoreOrders.FirstOrDefaultAsync(o => o.Id == c.OrderId && o.StudentId == c.StudentId, ct) is not { } order) return StoreErrors.OrderNotFound;
        if (order.Status != OrderStatus.Placed) return Error.Rule("order.state", "This order is already being handled. Contact the school office.");
        if (order.Cancel("Cancelled by the parent", clock.Now.ToUniversalTime()) is { } invalid) return invalid.ToError();
        var ids = order.Lines.Select(l => l.VariantId).ToList();
        orders.Release(order, await db.StoreVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct), "Cancelled by the parent");
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([order], ct))[0];
    }
}
