using Maydel.Application.Abstractions;
using Maydel.Domain.Communication;

namespace Maydel.Application.Notifications;

/// <summary>Which department an email comes from (E12.7), so families can tell admissions, fees and results mail apart and reply to the right office.</summary>
public static class Senders
{
    public static EmailFrom For(EmailKind kind, EmailOptions o)
    {
        var (address, label) = kind switch
        {
            EmailKind.AdmissionStage or EmailKind.AdmissionOffer => (o.AdmissionsAddress, "Admissions"),
            EmailKind.InvoicePublished or EmailKind.ProofConfirmed or EmailKind.ProofRejected => (o.AccountsAddress, "Accounts"),
            EmailKind.ReportPublished => (o.ResultsAddress, "Results"),
            _ => (null, null),
        };
        return string.IsNullOrWhiteSpace(address)
            ? new EmailFrom(o.FromAddress, o.FromName)
            : new EmailFrom(address.Trim(), label is null ? o.FromName : $"{o.FromName} {label}");
    }
}
