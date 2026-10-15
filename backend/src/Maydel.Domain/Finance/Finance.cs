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
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    public decimal Total => Lines.Sum(l => l.Amount);
    public decimal AmountPaid => Payments.Sum(p => p.Amount);
    public decimal Balance => Total - AmountPaid;

    public InvoiceStatus Status => IsCancelled ? InvoiceStatus.Cancelled
        : AmountPaid <= 0 ? InvoiceStatus.Unpaid
        : AmountPaid < Total ? InvoiceStatus.PartiallyPaid
        : InvoiceStatus.Paid;

    public Payment RecordPayment(decimal amount, PaymentMethod method, string? reference, DateOnly paidOn, Guid? receivedBy)
    {
        if (IsCancelled) throw new DomainException("Cannot record a payment on a cancelled invoice.");
        if (amount <= 0) throw new DomainException("Payment amount must be greater than zero.");
        if (amount > Balance) throw new DomainException($"Payment of {amount:N2} exceeds the outstanding balance of {Balance:N2}.");

        var payment = new Payment
        {
            InvoiceId = Id,
            Amount = amount,
            Method = method,
            Reference = reference,
            PaidOn = paidOn,
            ReceivedByUserId = receivedBy,
            ReceiptNumber = $"RCT-{Guid.CreateVersion7().ToString("N")[..10].ToUpperInvariant()}",
        };
        Payments.Add(payment);
        return payment;
    }
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
}
