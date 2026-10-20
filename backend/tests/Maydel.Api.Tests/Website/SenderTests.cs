using Maydel.Domain.Communication;

namespace Maydel.Api.Tests.Website;

public sealed class SenderTests
{
    [Fact]
    public void Each_department_sends_from_its_own_address_when_configured()
    {
        var o = new Maydel.Application.Abstractions.EmailOptions { FromAddress = "no-reply@school.ng", FromName = "Maydel", AdmissionsAddress = "admissions@school.ng" };
        Assert.Equal(("admissions@school.ng", "Maydel Admissions"), Pair(Maydel.Application.Notifications.Senders.For(EmailKind.AdmissionOffer, o)));
        Assert.Equal(("no-reply@school.ng", "Maydel"), Pair(Maydel.Application.Notifications.Senders.For(EmailKind.InvoicePublished, o))); // accounts@ not set
        o.AccountsAddress = "accounts@school.ng";
        Assert.Equal(("accounts@school.ng", "Maydel Accounts"), Pair(Maydel.Application.Notifications.Senders.For(EmailKind.ProofConfirmed, o)));
        Assert.Equal(("no-reply@school.ng", "Maydel"), Pair(Maydel.Application.Notifications.Senders.For(EmailKind.ContactForm, o)));
    }

    private static (string, string) Pair(Maydel.Application.Abstractions.EmailFrom f) => (f.Address, f.Name);
}
