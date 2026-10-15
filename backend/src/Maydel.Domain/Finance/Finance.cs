using Maydel.Domain.Academics;
using Maydel.Domain.Common;
using Maydel.Domain.People;

namespace Maydel.Domain.Finance;

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    Pos,
    Online,
}

public enum InvoiceStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,
    Cancelled,
}

/// <summary>A billable item for a term, e.g. "Tuition" for Primary or "Bus" (optional).</summary>
public class FeeItem : Entity
{
    public required string Name { get; set; }
    public decimal Amount { get; set; }
    public Guid TermId { get; set; }
    public Term Term { get; set; } = null!;

    /// <summary>Null applies the fee to every class level.</summary>
    public Guid? ClassLevelId { get; set; }
    public ClassLevel? ClassLevel { get; set; }

    /// <summary>Mandatory items go on every invoice; optional ones (bus, lunch) only for students opted in.</summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>The sibling discount is calculated on these items (typically tuition).</summary>
    public bool SiblingDiscount { get; set; }

    public List<FeeOptIn> OptIns { get; set; } = [];
}

/// <summary>A student who takes an optional fee item (E10.1, per-student opt-in).</summary>
public class FeeOptIn
{
    public Guid FeeItemId { get; set; }
    public Guid StudentId { get; set; }
}

public enum SiblingDiscountMode
{
    None,
    /// <summary>Second child gets <see cref="FeeSettings.SecondChild"/>% off, third and later <see cref="FeeSettings.ThirdAndLater"/>%.</summary>
    Percentage,
    /// <summary>A fixed naira amount off for the second child, and for each later child.</summary>
    FixedAmount,
}

/// <summary>School-wide fee rules, chosen by the admin (one row).</summary>
public class FeeSettings : Entity
{
    public SiblingDiscountMode SiblingMode { get; set; }
    public decimal SecondChild { get; set; }
    public decimal ThirdAndLater { get; set; }
}

/// <summary>
/// Instalments for a term, or for every term of a session (a term's own plan wins). 2 or 3 parts that add up to 100%,
/// due in date order. When there's no enabled plan, fees are due in full.
/// </summary>
public class InstalmentPlan : Entity
{
    public Guid? TermId { get; set; }
    public Guid? SessionId { get; set; }
    public bool Enabled { get; set; } = true;
    public List<Instalment> Instalments { get; set; } = [];
}

public class Instalment
{
    public Guid PlanId { get; set; }
    public int Order { get; set; }
    public decimal Percent { get; set; }
    public DateOnly DueOn { get; set; }
}

/// <summary>Fee rules (E10.1–E10.2). Pure, unit-tested.</summary>
public static class FeeRules
{
    public const decimal MaxAmount = 100_000_000m;

    public static DomainError? CheckFeeItem(string name, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(name)) return new("fee.name", "Give the fee a name.", "name");
        if (amount <= 0 || amount > MaxAmount || decimal.Round(amount, 2) != amount)
            return new("fee.amount", "The amount must be above ₦0 with at most 2 decimal places.", "amount");
        return null;
    }

    public static DomainError? CheckSiblingSettings(SiblingDiscountMode mode, decimal second, decimal thirdAndLater)
    {
        if (second < 0 || thirdAndLater < 0) return new("fee.sibling_negative", "Discounts can't be negative.", "secondChild");
        if (mode == SiblingDiscountMode.Percentage && (second > 100 || thirdAndLater > 100))
            return new("fee.sibling_percent", "A percentage discount must be between 0 and 100.", "secondChild");
        if (decimal.Round(second, 2) != second || decimal.Round(thirdAndLater, 2) != thirdAndLater)
            return new("fee.sibling_decimals", "Use at most 2 decimal places.", "secondChild");
        return null;
    }

    /// <summary>
    /// Order within a family: the eldest (earliest date of birth; unknown birthdays last, then earliest admission) is
    /// child 1 and pays in full; the next is child 2, and so on.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> SiblingRanks(IEnumerable<(Guid Id, DateOnly? DateOfBirth, DateOnly AdmittedOn)> family) =>
        family.OrderBy(c => c.DateOfBirth ?? DateOnly.MaxValue).ThenBy(c => c.AdmittedOn).ThenBy(c => c.Id)
            .Select((c, i) => (c.Id, Rank: i + 1)).ToDictionary(x => x.Id, x => x.Rank);

    /// <summary>The discount for the child at <paramref name="rank"/> on the discount-eligible part of their fees.</summary>
    public static decimal SiblingDiscount(SiblingDiscountMode mode, decimal second, decimal thirdAndLater, int rank, decimal eligibleTotal)
    {
        if (mode == SiblingDiscountMode.None || rank < 2 || eligibleTotal <= 0) return 0;
        var value = rank == 2 ? second : thirdAndLater;
        var discount = mode == SiblingDiscountMode.Percentage
            ? Math.Round(eligibleTotal * value / 100m, 2, MidpointRounding.AwayFromZero)
            : value;
        return Math.Min(discount, eligibleTotal);
    }

    public static DomainError? CheckInstalments(IReadOnlyList<(decimal Percent, DateOnly DueOn)> parts)
    {
        if (parts.Count is < 2 or > 3) return new("fee.instalment_count", "Use 2 or 3 instalments.", "instalments");
        if (parts.Any(p => p.Percent <= 0 || decimal.Round(p.Percent, 2) != p.Percent))
            return new("fee.instalment_percent", "Each instalment needs a share above 0%.", "instalments");
        if (parts.Sum(p => p.Percent) != 100) return new("fee.instalment_total", "The instalments must add up to 100%.", "instalments");
        for (var i = 1; i < parts.Count; i++)
            if (parts[i].DueOn <= parts[i - 1].DueOn) return new("fee.instalment_dates", "Each instalment must be due after the one before.", "instalments");
        return null;
    }

    /// <summary>Amounts per instalment, rounded to kobo; the last absorbs the rounding so they add up exactly.</summary>
    public static IReadOnlyList<decimal> Split(decimal total, IReadOnlyList<decimal> percents)
    {
        var amounts = percents.Select(p => Math.Round(total * p / 100m, 2, MidpointRounding.AwayFromZero)).ToList();
        amounts[^1] = total - amounts.Take(amounts.Count - 1).Sum();
        return amounts;
    }

    /// <summary>"2026/2027" and 7 → "INV-2026-2027-0007".</summary>
    public static string InvoiceNumber(string sessionName, int sequence) => $"INV-{sessionName.Replace('/', '-')}-{sequence:0000}";

    /// <summary>
    /// The least a payment must be to complete the next unpaid instalment, given what's already paid. Instalments are
    /// cumulative milestones: with ₦60k + ₦40k, nothing paid needs ₦60k; ₦70k paid needs ₦30k more.
    /// </summary>
    public static decimal NextInstalmentMinimum(IReadOnlyList<decimal> instalments, decimal paid)
    {
        decimal milestone = 0;
        foreach (var part in instalments)
        {
            milestone += part;
            if (milestone > paid) return milestone - paid;
        }
        return 0;
    }

    /// <summary>"2026/2027" and 12 → "RCT-2026-2027-00012". Receipts are numbered per session, gap-free in normal use.</summary>
    public static string ReceiptNumber(string sessionName, int sequence) => $"RCT-{sessionName.Replace('/', '-')}-{sequence:00000}";

    /// <summary>
    /// How much should have been paid by <paramref name="date"/>: the instalments due by then, or the whole total once
    /// <paramref name="dueOn"/> has passed when there's no plan.
    /// </summary>
    public static decimal DueBy(DateOnly date, decimal total, DateOnly? dueOn, IReadOnlyList<(decimal Amount, DateOnly DueOn)> schedule) =>
        schedule.Count > 0 ? schedule.Where(s => s.DueOn <= date).Sum(s => s.Amount)
        : dueOn is { } due && due <= date ? total : 0;
}

public class Invoice : Entity
{
    public required string Number { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    /// <summary>The student's class when invoiced, for class lists and reports.</summary>
    public Guid? ClassRoomId { get; set; }
    public Guid TermId { get; set; }
    public Term Term { get; set; } = null!;
    public DateOnly? DueOn { get; set; }
    public bool IsCancelled { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    public decimal Total => Lines.Sum(l => l.Amount);
    public decimal AmountPaid => Payments.Sum(p => p.Amount);
    public decimal Balance => Total - AmountPaid;

    public InvoiceStatus Status => IsCancelled ? InvoiceStatus.Cancelled
        : AmountPaid <= 0 ? InvoiceStatus.Unpaid
        : AmountPaid < Total ? InvoiceStatus.PartiallyPaid
        : InvoiceStatus.Paid;

    private static readonly DomainError Cancelled = new("invoice.cancelled", "This invoice is cancelled.");

    /// <summary>
    /// Records a payment (E10.3). It can't exceed the balance. Without an instalment plan (<paramref name="instalments"/>
    /// empty) the balance must be paid in full. With one, each payment must at least complete the next instalment:
    /// with 60% / 40%, the first payment is at least 60% and the next at least the remaining 40%; paying more is fine.
    /// Receipts are never edited afterwards: mistakes are reversed.
    /// </summary>
    public (Payment? Payment, DomainError? Error) RecordPayment(
        decimal amount, PaymentMethod method, string? reference, DateOnly paidOn, Guid? receivedBy, string receiptNumber, IReadOnlyList<decimal> instalments)
    {
        if (IsCancelled) return (null, Cancelled);
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            return (null, new("payment.amount", "Enter an amount above ₦0 with at most 2 decimal places.", "amount"));
        if (amount > Balance)
            return (null, new("payment.exceeds_balance", $"That's more than the outstanding balance of ₦{Balance:N2}.", "amount"));
        if (instalments.Count == 0 && amount != Balance)
            return (null, new("payment.full_required", $"Instalments aren't enabled for this term: the full balance of ₦{Balance:N2} is due.", "amount"));
        if (instalments.Count > 0 && amount != Balance && amount < FeeRules.NextInstalmentMinimum(instalments, AmountPaid))
            return (null, new("payment.instalment_minimum",
                $"Pay at least ₦{FeeRules.NextInstalmentMinimum(instalments, AmountPaid):N2} to complete the next instalment, or the full balance of ₦{Balance:N2}.", "amount"));

        var payment = new Payment
        {
            InvoiceId = Id, Amount = amount, Method = method, Reference = Clean(reference), PaidOn = paidOn,
            ReceivedByUserId = receivedBy, ReceiptNumber = receiptNumber,
        };
        Payments.Add(payment);
        return (payment, null);
    }

    /// <summary>Undoes a payment with an equal and opposite entry; the original receipt stays as it was.</summary>
    public (Payment? Reversal, DomainError? Error) Reverse(Payment original, string reason, DateOnly on, Guid? by, string receiptNumber)
    {
        if (original.IsReversal) return (null, new("payment.is_reversal", "A reversal can't itself be reversed."));
        if (Payments.Any(p => p.ReversesPaymentId == original.Id)) return (null, new("payment.already_reversed", "That payment has already been reversed."));
        if (string.IsNullOrWhiteSpace(reason)) return (null, new("payment.reason", "Say why the payment is being reversed.", "reason"));

        var reversal = new Payment
        {
            InvoiceId = Id, Amount = -original.Amount, Method = original.Method, Reference = original.ReceiptNumber, PaidOn = on,
            ReceivedByUserId = by, ReceiptNumber = receiptNumber, ReversesPaymentId = original.Id, Note = reason.Trim(),
        };
        Payments.Add(reversal);
        return (reversal, null);
    }

    /// <summary>Cancels an invoice that has no money on it (reverse payments first). The student can then be invoiced again.</summary>
    public DomainError? Cancel(string reason, DateTimeOffset at)
    {
        if (IsCancelled) return Cancelled;
        if (AmountPaid != 0) return new("invoice.has_payments", "This invoice has payments. Reverse them before cancelling it.");
        if (string.IsNullOrWhiteSpace(reason)) return new("invoice.reason", "Say why the invoice is being cancelled.", "reason");
        (IsCancelled, CancelledAt, CancelReason) = (true, at, reason.Trim());
        return null;
    }

    /// <summary>
    /// Adds a line after invoicing: negative for a scholarship or one-off discount, positive for an extra charge.
    /// The total can't drop below what's already been paid.
    /// </summary>
    public DomainError? Adjust(string description, decimal amount)
    {
        if (IsCancelled) return Cancelled;
        if (string.IsNullOrWhiteSpace(description)) return new("invoice.adjustment_description", "Describe the adjustment.", "description");
        if (amount == 0 || decimal.Round(amount, 2) != amount)
            return new("invoice.adjustment_amount", "Enter a non-zero amount with at most 2 decimal places.", "amount");
        if (Total + amount < AmountPaid)
            return new("invoice.adjustment_below_paid", $"That would bring the total below the ₦{AmountPaid:N2} already paid.", "amount");
        Lines.Add(new InvoiceLine { Description = description.Trim(), Amount = amount });
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public class InvoiceLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid InvoiceId { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
}

public class Payment : Entity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public required string ReceiptNumber { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public DateOnly PaidOn { get; set; }
    public Guid? ReceivedByUserId { get; set; }

    /// <summary>Set on a reversal entry: the payment it undoes. Reversals have a negative amount.</summary>
    public Guid? ReversesPaymentId { get; set; }
    public string? Note { get; set; }

    public bool IsReversal => ReversesPaymentId is not null;
}
