using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Domain.Admissions;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Admissions.StartFormPayment;

internal sealed class StartFormPaymentHandler(IAppDbContext db, IPaymentGateway gateway, IOptions<PaystackOptions> options, ICurrentUser me)
    : ICommandHandler<StartFormPaymentCommand, OnlineCheckout>
{
    public async Task<Result<OnlineCheckout>> Handle(StartFormPaymentCommand c, CancellationToken ct)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == c.ApplicationId && a.ApplicantUserId == me.UserId, ct);
        if (application is null) return AdmissionErrors.ApplicationNotFound;
        if (application.Status != ApplicationStatus.AwaitingPayment) return AdmissionErrors.NotAwaitingPayment;
        if (!gateway.IsConfigured) return FinanceErrors.OnlineUnavailable;
        if (application.FormFee <= 0) return Error.Rule("admission.free_form", "This form has no fee; ask the school for a form code.");

        var reference = $"MSF-{Guid.CreateVersion7():N}";
        db.FormPayments.Add(new FormPayment { Reference = reference, ApplicationId = application.Id, Amount = application.FormFee });
        await db.SaveChangesAsync(ct);
        var checkout = await gateway.StartAsync(application.Email, OnlinePayment.ToKobo(application.FormFee), reference, options.Value.AdmissionsCallbackUrl,
            new Dictionary<string, string> { ["application"] = application.ReferenceNumber, ["child"] = application.ChildName, ["purpose"] = "admission form" }, ct);
        return new OnlineCheckout(reference, checkout.AuthorizationUrl, application.FormFee);
    }
}
