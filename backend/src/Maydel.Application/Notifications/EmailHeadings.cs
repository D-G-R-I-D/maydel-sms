using Maydel.Domain.Communication;

namespace Maydel.Application.Notifications;

/// <summary>The title shown at the top of each kind of branded email.</summary>
public static class EmailHeadings
{
    public static string? For(EmailKind kind) => kind switch
    {
        EmailKind.ReportPublished => "Report card",
        EmailKind.InvoicePublished => "Fees invoice",
        EmailKind.ProofConfirmed => "Payment confirmed",
        EmailKind.ProofRejected => "Proof of payment not confirmed",
        EmailKind.AdmissionStage => "Admissions",
        EmailKind.AdmissionOffer => "Admission offer",
        EmailKind.ContactForm => "Website message",
        EmailKind.PaymentReceipt => "Payment received",
        EmailKind.FeesOverdue => "Fees overdue",
        EmailKind.NewsletterConfirm => "Confirm your subscription",
        EmailKind.BookDigest => "Communication book",
        _ => null,
    };
}
