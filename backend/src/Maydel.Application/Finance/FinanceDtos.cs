using Maydel.Application.Abstractions;
using Maydel.Domain.Academics;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance;

/// <summary>Sibling discount rule. Percentage: values are %; FixedAmount: values are naira. Child 1 (eldest) pays in full.</summary>
public sealed record FeeSettingsDto(SiblingDiscountMode SiblingMode, decimal SecondChild, decimal ThirdAndLater);

/// <summary><c>OptedIn</c>: students taking an optional item (always 0 for mandatory items).</summary>
public sealed record FeeItemDto(
    Guid Id, Guid TermId, string Name, decimal Amount, Guid? ClassLevelId, string? ClassLevel, bool IsMandatory, bool SiblingDiscount, int OptedIn);

public sealed record FeeItemInput(string Name, decimal Amount, Guid? ClassLevelId, bool IsMandatory, bool SiblingDiscount);

public sealed record OptInStudent(Guid StudentId, string AdmissionNumber, string Name, string? ClassRoom, bool OptedIn);

public enum PlanScope
{
    None,
    Term,
    Session,
}

/// <summary>The plan that applies to a term: its own (<c>Term</c>), its session's (<c>Session</c>), or none (pay in full).</summary>
public sealed record InstalmentPlanDto(Guid TermId, PlanScope Scope, bool Enabled, IReadOnlyList<InstalmentDto> Instalments);

public sealed record InstalmentDto(int Order, decimal Percent, DateOnly DueOn);

public sealed record InstalmentInput(decimal Percent, DateOnly DueOn);

/// <summary><c>NoFees</c>: active students with nothing to bill (no fee items for their level).</summary>
public sealed record GenerateInvoicesResult(int Created, int AlreadyInvoiced, int NoFees, decimal TotalBilled);

public sealed record InvoiceSummary(
    Guid Id, string Number, Guid StudentId, string AdmissionNumber, string StudentName, string? ClassRoom, Guid TermId, string Term,
    decimal Total, decimal Paid, decimal Balance, InvoiceStatus Status, DateOnly? DueOn, DateTimeOffset IssuedAt);

public sealed record InvoiceLineDto(string Description, decimal Amount);

/// <summary>An instalment of this invoice: what's due by when. Empty schedule = due in full by <c>DueOn</c>.</summary>
public sealed record ScheduleItem(int Order, decimal Percent, decimal Amount, DateOnly DueOn);

public sealed record InvoiceDetail(
    Guid Id, string Number, Guid StudentId, string AdmissionNumber, string StudentName, string? ClassRoom, Guid TermId, string Term, string Session,
    IReadOnlyList<InvoiceLineDto> Lines, decimal Total, decimal Paid, decimal Balance, InvoiceStatus Status, DateOnly? DueOn,
    IReadOnlyList<ScheduleItem> Schedule, DateTimeOffset IssuedAt,
    IReadOnlyList<PaymentDto> Payments, bool InstalmentsAllowed, decimal DueNow, decimal Overdue, string? CancelReason, decimal MinimumPayment);

/// <summary>
/// A receipt or a reversal (negative amount, <c>ReversesReceipt</c> set). <c>ReversedBy</c>: the receipt number of the
/// reversal that undid this payment.
/// </summary>
public sealed record PaymentDto(
    Guid Id, string ReceiptNumber, decimal Amount, PaymentMethod Method, string? Reference, DateOnly PaidOn, bool IsReversal,
    string? ReversesReceipt, string? ReversedBy, string? Note, DateTimeOffset RecordedAt);

/// <summary>What the bursar hands over or prints: who paid what against which invoice, and what's left.</summary>
public sealed record ReceiptDto(
    Guid PaymentId, string ReceiptNumber, decimal Amount, PaymentMethod Method, string? Reference, DateOnly PaidOn, bool IsReversal,
    Guid InvoiceId, string InvoiceNumber, string AdmissionNumber, string StudentName, string? ClassRoom, string Term, decimal BalanceAfter);

/// <summary>One line of a student's account, oldest first. <c>Balance</c> is the running amount owed.</summary>
public sealed record StatementEntry(DateOnly Date, string Kind, string Reference, string Description, decimal Debit, decimal Credit, decimal Balance);

public sealed record StudentStatement(
    Guid StudentId, string AdmissionNumber, string Name, string? ClassRoom, decimal TotalBilled, decimal TotalPaid, decimal Balance,
    IReadOnlyList<StatementEntry> Entries);

/// <summary><c>DueNow</c>: what should have been paid by today; <c>Overdue</c>: the part of it still unpaid.</summary>
public sealed record DebtorRow(
    Guid StudentId, string AdmissionNumber, string Name, string? ClassRoom, Guid InvoiceId, string InvoiceNumber,
    decimal Total, decimal Paid, decimal Balance, decimal DueNow, decimal Overdue, DateOnly? NextDueOn, string? Guardian, string? GuardianPhone);

public sealed record DebtorsReport(Guid TermId, string Term, int Count, decimal TotalOutstanding, decimal TotalOverdue, IReadOnlyList<DebtorRow> Rows);

/// <summary>Where the parent's browser goes to pay, and our reference to confirm it afterwards.</summary>
public sealed record OnlineCheckout(string Reference, string AuthorizationUrl, decimal Amount);

/// <summary>An online payment's outcome. <c>ReceiptNumber</c> once recorded; <c>Excess</c> when money needs refunding.</summary>
public sealed record OnlinePaymentResult(
    string Reference, OnlinePaymentStatus Status, decimal Amount, Guid InvoiceId, Guid StudentId, string? ReceiptNumber, Guid? PaymentId, decimal Excess, string? Message);

public sealed record OnlinePaymentRow(
    Guid Id, string Reference, OnlinePaymentStatus Status, decimal Amount, decimal Excess, Guid InvoiceId, string InvoiceNumber,
    string AdmissionNumber, string StudentName, string? ReceiptNumber, string? Message, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt);

public static class FinanceErrors
{
    public static readonly Common.Error OnlineUnavailable = Common.Error.Rule("payment.online_unavailable", "Online payment isn't set up yet. Please pay at the school or by bank transfer.");
    public static readonly Common.Error NothingOwed = Common.Error.Rule("invoice.nothing_owed", "There's nothing left to pay on this invoice.");
    public static readonly Common.Error OnlineNotFound = Common.Error.NotFound("payment.online_not_found", "We couldn't find that payment.");
    public static readonly Common.Error TermNotFound = Common.Error.NotFound("term.not_found", "That term doesn't exist.");
    public static readonly Common.Error FeeItemNotFound = Common.Error.NotFound("fee.not_found", "That fee item doesn't exist.");
    public static readonly Common.Error ClassLevelNotFound = Common.Error.Validation("classLevelId", "That class level doesn't exist.");
    public static readonly Common.Error InvoiceNotFound = Common.Error.NotFound("invoice.not_found", "That invoice doesn't exist.");
    public static readonly Common.Error MandatoryNoOptIn = Common.Error.Rule("fee.mandatory", "Everyone pays a mandatory item; opt-ins are only for optional items.");
    public static readonly Common.Error PaymentNotFound = Common.Error.NotFound("payment.not_found", "That payment doesn't exist.");
    public static readonly Common.Error StudentNotFound = Common.Error.NotFound("student.not_found", "That student doesn't exist.");
    public static readonly Common.Error FuturePayment = Common.Error.Validation("paidOn", "A payment can't be dated in the future.");
    public static readonly Common.Error SameTerm = Common.Error.Validation("toTermId", "Choose a different term to copy to.");
}

internal static class FinanceQueries
{
    public static async Task<FeeSettings> SettingsAsync(this IAppDbContext db, CancellationToken ct) =>
        await db.FeeSettings.OrderBy(s => s.CreatedAt).FirstOrDefaultAsync(ct) ?? new FeeSettings();

    /// <summary>The term's own plan, else its session's; null when neither exists.</summary>
    public static async Task<(InstalmentPlan Plan, PlanScope Scope)?> PlanForAsync(this IAppDbContext db, Term term, CancellationToken ct)
    {
        var own = await db.InstalmentPlans.AsNoTracking().FirstOrDefaultAsync(p => p.TermId == term.Id, ct);
        if (own is not null) return (own, PlanScope.Term);
        var session = await db.InstalmentPlans.AsNoTracking().FirstOrDefaultAsync(p => p.SessionId == term.SessionId, ct);
        return session is null ? null : (session, PlanScope.Session);
    }

    public static IQueryable<FeeItemDto> FeeItemDtos(this IQueryable<FeeItem> items) =>
        items.OrderBy(f => f.ClassLevelId == null ? 0 : 1).ThenBy(f => f.ClassLevel!.Order).ThenBy(f => !f.IsMandatory).ThenBy(f => f.Name)
            .Select(f => new FeeItemDto(f.Id, f.TermId, f.Name, f.Amount, f.ClassLevelId, f.ClassLevel != null ? f.ClassLevel.Name : null,
                f.IsMandatory, f.SiblingDiscount, f.IsMandatory ? 0 : f.OptIns.Count));
}
