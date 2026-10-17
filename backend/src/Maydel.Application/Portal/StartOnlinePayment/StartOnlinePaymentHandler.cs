using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Portal.StartOnlinePayment;

internal sealed class StartOnlinePaymentHandler(
    IAppDbContext db, AccessRules access, IPaymentGateway gateway, IOptions<PaystackOptions> options, ISchoolClock clock, ICurrentUser me)
    : ICommandHandler<StartOnlinePaymentCommand, OnlineCheckout>
{
    public async Task<Result<OnlineCheckout>> Handle(StartOnlinePaymentCommand c, CancellationToken ct)
    {
        if (!await access.PortalStudents().AnyAsync(s => s.Id == c.StudentId, ct)) return PortalErrors.StudentNotFound;
        var owned = await db.Invoices.AnyAsync(i => i.Id == c.InvoiceId && i.StudentId == c.StudentId && !i.IsCancelled && i.PublishedAt != null, ct);
        if (!owned || await db.DetailAsync(c.InvoiceId, clock.Today, ct) is not { } invoice) return PortalErrors.InvoiceNotFound;
        if (!gateway.IsConfigured) return FinanceErrors.OnlineUnavailable;
        if (invoice.Balance <= 0) return FinanceErrors.NothingOwed;

        var amount = c.Amount ?? invoice.Balance;
        if (amount <= 0 || decimal.Round(amount, 2) != amount || amount > invoice.Balance)
            return Error.Validation("amount", $"Enter an amount up to the balance of ₦{invoice.Balance:N2}.");
        if (amount != invoice.Balance && amount < invoice.MinimumPayment)
            return Error.Validation("amount", $"Pay at least ₦{invoice.MinimumPayment:N2} to complete the next instalment, or the full balance of ₦{invoice.Balance:N2}.");

        // Paystack emails its own receipt: the parent's address, else the school's so it's never lost.
        var email = await db.Guardians.Where(g => g.UserId == me.UserId && g.Email != null).Select(g => g.Email).FirstOrDefaultAsync(ct)
            ?? await db.Users.Where(u => u.Id == me.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct)
            ?? await db.SchoolProfiles.Select(p => p.Email).FirstOrDefaultAsync(ct)
            ?? "accounts@maydelspringschools.com";

        var reference = $"MSS-{Guid.CreateVersion7():N}";
        db.OnlinePayments.Add(new OnlinePayment
        {
            Reference = reference, InvoiceId = invoice.Id, StudentId = c.StudentId, Amount = amount, InitiatedByUserId = me.UserId,
        });
        await db.SaveChangesAsync(ct);

        var checkout = await gateway.StartAsync(email, OnlinePayment.ToKobo(amount), reference, options.Value.CallbackUrl,
            new Dictionary<string, string> { ["invoice"] = invoice.Number, ["admission_number"] = invoice.AdmissionNumber, ["student"] = invoice.StudentName }, ct);
        return new OnlineCheckout(reference, checkout.AuthorizationUrl, amount);
    }
}
