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
    public bool IsMandatory { get; set; } = true;
}

public class Invoice : Entity
{
    public required string Number { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
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
