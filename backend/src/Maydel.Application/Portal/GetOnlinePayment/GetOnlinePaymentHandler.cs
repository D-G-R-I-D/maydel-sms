using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Application.Finance.ConfirmOnlinePayment;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Portal.GetOnlinePayment;

internal sealed class GetOnlinePaymentHandler(IAppDbContext db, AccessRules access, ICommandHandler<ConfirmOnlinePaymentCommand, OnlinePaymentResult> confirm)
    : IQueryHandler<GetOnlinePaymentQuery, OnlinePaymentResult>
{
    public async Task<Result<OnlinePaymentResult>> Handle(GetOnlinePaymentQuery q, CancellationToken ct)
    {
        var studentId = await db.OnlinePayments.Where(o => o.Reference == q.Reference).Select(o => (Guid?)o.StudentId).FirstOrDefaultAsync(ct);
        if (studentId is null || !await access.PortalStudents().AnyAsync(s => s.Id == studentId, ct)) return FinanceErrors.OnlineNotFound;
        return await confirm.Handle(new ConfirmOnlinePaymentCommand(q.Reference), ct);
    }
}
