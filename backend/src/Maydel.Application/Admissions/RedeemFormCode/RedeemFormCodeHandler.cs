using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.RedeemFormCode;

internal sealed class RedeemFormCodeHandler(IAppDbContext db, FormPaid paid, TimeProvider time, ICurrentUser me) : ICommandHandler<RedeemFormCodeCommand, ApplicationDto>
{
    public async Task<Result<ApplicationDto>> Handle(RedeemFormCodeCommand c, CancellationToken ct)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == c.ApplicationId && a.ApplicantUserId == me.UserId, ct);
        if (application is null) return AdmissionErrors.ApplicationNotFound;
        if (application.Status != ApplicationStatus.AwaitingPayment) return AdmissionErrors.NotAwaitingPayment;
        var normalised = FormCode.Normalise(c.Code);
        var code = normalised is null ? null
            : await db.FormCodes.FirstOrDefaultAsync(f => f.Code == normalised && f.SessionId == application.SessionId && f.UsedByApplicationId == null, ct);
        if (code is null) return AdmissionErrors.BadCode;

        (code.UsedByApplicationId, code.UsedAt) = (application.Id, time.GetUtcNow());
        application.FormFee = code.AmountPaid;
        if (await paid.ApplyAsync(application, FormPaymentMethod.SchoolCode, code.Code, ct) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return await db.Dtos(db.Applications.Where(a => a.Id == application.Id)).FirstAsync(ct);
    }
}
