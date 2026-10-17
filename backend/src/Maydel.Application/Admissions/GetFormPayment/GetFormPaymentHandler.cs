using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.GetFormPayment;

internal sealed class GetFormPaymentHandler(IAppDbContext db, FormPayments payments, ICurrentUser me) : IQueryHandler<GetFormPaymentQuery, FormPayments.Outcome>
{
    public async Task<Result<FormPayments.Outcome>> Handle(GetFormPaymentQuery q, CancellationToken ct)
    {
        var mine = await db.FormPayments.AnyAsync(f => f.Reference == q.Reference
            && db.Applications.Any(a => a.Id == f.ApplicationId && a.ApplicantUserId == me.UserId), ct);
        if (!mine) return Finance.FinanceErrors.OnlineNotFound;
        if (await payments.ConfirmAsync(q.Reference, ct) is { } outcome) return outcome;
        var stored = await db.FormPayments.AsNoTracking().FirstAsync(f => f.Reference == q.Reference, ct);
        return new FormPayments.Outcome(stored.Reference, stored.Status, stored.ApplicationId, stored.Message);
    }
}
