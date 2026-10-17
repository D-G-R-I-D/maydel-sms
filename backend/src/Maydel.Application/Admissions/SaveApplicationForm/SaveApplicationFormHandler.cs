using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SaveApplicationForm;

internal sealed class SaveApplicationFormHandler(IAppDbContext db, ICurrentUser me) : ICommandHandler<SaveApplicationFormCommand, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(SaveApplicationFormCommand c, CancellationToken ct)
    {
        if (await db.MineAsync(c.ApplicationId, me.UserId, ct) is not { } application) return AdmissionErrors.ApplicationNotFound;
        var b = c.Body;
        if (application.SaveForm(b.Form ?? new(), b.ChildFirstName ?? "", b.ChildLastName ?? "", b.ChildDateOfBirth) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await db.DetailAsync(application.Id, ct))!;
    }
}
