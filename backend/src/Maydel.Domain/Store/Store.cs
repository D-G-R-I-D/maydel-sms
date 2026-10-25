using Maydel.Domain.Academics;
using Maydel.Domain.Common;

namespace Maydel.Domain.Store;

/// <summary>Which sections an item is for; none = every section.</summary>
[Flags]
public enum StoreSections
{
    None = 0,
    Creche = 1,
    Nursery = 2,
    Primary = 4,
    JuniorSecondary = 8,
    SeniorSecondary = 16,
}

public enum StoreCategory
{
    Uniform,
    Sportswear,
    Books,
    IdCard,
    Other,
}

/// <summary>Something the school sells (E19.1), in one or more variants (sizes, house colours) with their own price and stock.</summary>
public class StoreItem : Entity
{
    public required string Name { get; set; }
    public StoreCategory Category { get; set; }
    public string? Description { get; set; }
    public StoreSections Sections { get; set; }
    public string? ImagePath { get; set; }

    /// <summary>On sale (in the office and the parent shop). Needs every active variant priced.</summary>
    public bool IsActive { get; set; }
    public List<StoreVariant> Variants { get; set; } = [];

    public static StoreSections SectionOf(SchoolSection section) => section switch
    {
        SchoolSection.Creche => StoreSections.Creche,
        SchoolSection.Nursery => StoreSections.Nursery,
        SchoolSection.Primary => StoreSections.Primary,
        SchoolSection.JuniorSecondary => StoreSections.JuniorSecondary,
        _ => StoreSections.SeniorSecondary,
    };

    public bool IsFor(SchoolSection? section) => Sections == StoreSections.None || (section is { } s && Sections.HasFlag(SectionOf(s)));

    public DomainError? Check()
    {
        Name = Name.Trim();
        if (Name.Length is 0 or > 120) return new("store.name", "Give the item a name (up to 120 characters).", "name");
        if (Description is { Length: > 1000 }) return new("store.description", "Keep the description under 1,000 characters.", "description");
        if (Variants.Count == 0) return new("store.variants", "Add at least one size or option.", "variants");
        if (Variants.Any(v => string.IsNullOrWhiteSpace(v.Label) || v.Label.Trim().Length > 60)) return new("store.variant_label", "Every option needs a label (up to 60 characters).", "variants");
        if (Variants.GroupBy(v => v.Label.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) return new("store.variant_duplicate", "Two options have the same label.", "variants");
        if (Variants.Any(v => v.Price < 0 || decimal.Round(v.Price, 2) != v.Price)) return new("store.price", "Prices must be zero or more, with at most 2 decimal places.", "variants");
        if (Variants.Any(v => v.LowStockAt < 0)) return new("store.low_stock", "The low-stock level can't be negative.", "variants");
        if (IsActive && Variants.Any(v => v.IsActive && v.Price == 0)) return new("store.unpriced", "Set a price for every option before putting the item on sale.", "isActive");
        return null;
    }
}

public class StoreVariant : Entity
{
    public Guid ItemId { get; set; }
    public required string Label { get; set; }
    public decimal Price { get; set; }

    /// <summary>Available to sell (orders waiting for collection have already taken theirs).</summary>
    public int Stock { get; set; }

    /// <summary>At or below this, the item shows on the low-stock list.</summary>
    public int LowStockAt { get; set; } = 5;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum MovementKind
{
    Restock,
    Sale,
    Adjustment,

    /// <summary>A parent's order holds the stock until it's paid or released.</summary>
    Reserve,

    /// <summary>A cancelled or expired order gives its stock back.</summary>
    Release,
}

/// <summary>Every change to stock, with who and why (E19.2).</summary>
public class StockMovement : Entity
{
    public Guid VariantId { get; set; }
    public int Change { get; set; }
    public MovementKind Kind { get; set; }
    public string? Note { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Supplier { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ByUserId { get; set; }
}

public enum OrderStatus
{
    /// <summary>Placed; being prepared.</summary>
    Placed,

    /// <summary>Packed and waiting at the office.</summary>
    Ready,

    /// <summary>Handed over.</summary>
    Collected,
    Cancelled,
}

public enum OrderPayment
{
    Unpaid,
    Paid,

    /// <summary>Added to the student's fees invoice for the term.</summary>
    ChargedToFees,
}

public enum StorePayMethod
{
    Cash,
    BankTransfer,
    Pos,

    /// <summary>Added to the student's fees invoice.</summary>
    Fees,

    /// <summary>Parent shop: pays at the office when collecting.</summary>
    AtOffice,
}

public class StoreOrderLine
{
    public Guid VariantId { get; set; }
    public required string ItemName { get; set; }
    public required string VariantLabel { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>A sale to a student (E19.3): at the office, or ordered by a parent in the portal (E19.4).</summary>
public class StoreOrder : Entity
{
    public required string Number { get; set; }
    public Guid StudentId { get; set; }
    public Guid PlacedByUserId { get; set; }
    public required string PlacedByName { get; set; }
    public bool FromPortal { get; set; }
    public OrderStatus Status { get; set; }
    public OrderPayment Payment { get; set; }
    public StorePayMethod? PaidWith { get; set; }
    public string? PaymentReference { get; set; }
    public List<StoreOrderLine> Lines { get; set; } = [];
    public decimal Total { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? CollectedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>Unpaid parent orders are released after this (stock goes back).</summary>
    public DateTimeOffset? HoldUntil { get; set; }

    public bool IsOpen => Status is not (OrderStatus.Collected or OrderStatus.Cancelled);

    public DomainError? MarkPaid(StorePayMethod method, string? reference, DateTimeOffset now)
    {
        if (Status == OrderStatus.Cancelled) return new("order.cancelled", "This order was cancelled.");
        if (Payment != OrderPayment.Unpaid) return new("order.paid", "This order is already paid.");
        if (method is not (StorePayMethod.Cash or StorePayMethod.BankTransfer or StorePayMethod.Pos))
            return new("order.method", "Choose cash, bank transfer or POS.", "method");
        (Payment, PaidWith, PaymentReference, PaidAt, HoldUntil) = (OrderPayment.Paid, method, reference?.Trim(), now, null);
        return null;
    }

    public DomainError? MarkReady(DateTimeOffset now)
    {
        if (Status != OrderStatus.Placed) return new("order.state", "Only a new order can be marked ready.");
        (Status, ReadyAt) = (OrderStatus.Ready, now);
        return null;
    }

    public DomainError? MarkCollected(DateTimeOffset now)
    {
        if (Status is OrderStatus.Collected or OrderStatus.Cancelled) return new("order.state", "This order is already closed.");
        if (Payment == OrderPayment.Unpaid) return new("order.unpaid", "Take payment before handing the order over.");
        (Status, CollectedAt) = (OrderStatus.Collected, now);
        ReadyAt ??= now;
        return null;
    }

    public DomainError? Cancel(string? reason, DateTimeOffset now)
    {
        if (!IsOpen) return new("order.state", "This order is already closed.");
        if (Payment != OrderPayment.Unpaid) return new("order.paid_cancel", "A paid or fee-charged order can't be cancelled here. Refund or adjust the invoice first.");
        (Status, CancelledAt, CancelReason, HoldUntil) = (OrderStatus.Cancelled, now, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), null);
        return null;
    }
}

/// <summary>Store rules the admin can change.</summary>
public class StoreSettings : Entity
{
    /// <summary>Parents may add shop orders to the child's fees (otherwise they pay at the office).</summary>
    public bool ParentsCanChargeToFees { get; set; } = true;

    /// <summary>How long an unpaid parent order holds its stock.</summary>
    public int HoldHours { get; set; } = 48;
}
