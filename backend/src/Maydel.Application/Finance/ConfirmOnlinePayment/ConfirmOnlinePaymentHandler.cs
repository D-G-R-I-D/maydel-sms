using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ConfirmOnlinePayment;

internal sealed class ConfirmOnlinePaymentHandler(IAppDbContext db, OnlinePayments online) : ICommandHandler<ConfirmOnlinePaymentCommand, OnlinePaymentResult>
{
    public async Task<Result<OnlinePaymentResult>> Handle(ConfirmOnlinePaymentCommand c, CancellationToken ct)
    {
        if (await online.ConfirmAsync(c.Reference, ct) is { } result) return result;
        // Unknown reference, or recorded concurrently by the other path: read the stored outcome.
        var attempt = await db.OnlinePayments.AsNoTracking().FirstOrDefaultAsync(o => o.Reference == c.Reference, ct);
        return attempt is null ? FinanceErrors.OnlineNotFound : await online.ResultAsync(attempt, ct);
    }
}
