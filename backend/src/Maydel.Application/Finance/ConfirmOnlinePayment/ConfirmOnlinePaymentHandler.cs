using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ConfirmOnlinePayment;

internal sealed class ConfirmOnlinePaymentHandler(IAppDbContext db, OnlinePayments online, Admissions.FormPayments forms) : ICommandHandler<ConfirmOnlinePaymentCommand, OnlinePaymentResult>
{
    public async Task<Result<OnlinePaymentResult>> Handle(ConfirmOnlinePaymentCommand c, CancellationToken ct)
    {
        // Admission form payments ("MSF-…") share the webhook; they unlock the application instead of paying an invoice.
        if (c.Reference.StartsWith("MSF-", StringComparison.Ordinal))
            return await forms.ConfirmAsync(c.Reference, ct) is { } form
                ? new OnlinePaymentResult(form.Reference, (Domain.Finance.OnlinePaymentStatus)(int)form.Status, 0, Guid.Empty, Guid.Empty, null, null, 0, form.Message)
                : FinanceErrors.OnlineNotFound;

        if (await online.ConfirmAsync(c.Reference, ct) is { } result) return result;
        // Unknown reference, or recorded concurrently by the other path: read the stored outcome.
        var attempt = await db.OnlinePayments.AsNoTracking().FirstOrDefaultAsync(o => o.Reference == c.Reference, ct);
        return attempt is null ? FinanceErrors.OnlineNotFound : await online.ResultAsync(attempt, ct);
    }
}
