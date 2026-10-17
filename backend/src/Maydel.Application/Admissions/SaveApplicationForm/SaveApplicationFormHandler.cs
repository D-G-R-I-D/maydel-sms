using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.SaveApplicationForm;

internal sealed class SaveApplicationFormHandler(IAppDbContext db, ICurrentUser me) : ICommandHandler<SaveApplicationFormCommand, ApplicationDetail>
{
    public async Task<Result<ApplicationDetail>> Handle(SaveApplicationFormCommand c, CancellationToken ct)
    {
        var application = c.AsAdmin
            ? await db.Applications.FirstOrDefaultAsync(a => a.Id == c.ApplicationId, ct)
            : await db.MineAsync(c.ApplicationId, me.UserId, ct);
        if (application is null) return AdmissionErrors.ApplicationNotFound;
        var b = c.Body;
        var invalid = c.AsAdmin
            ? application.CorrectForm(b.Form ?? new(), b.ChildFirstName ?? "", b.ChildLastName ?? "", b.ChildDateOfBirth)
            : application.SaveForm(b.Form ?? new(), b.ChildFirstName ?? "", b.ChildLastName ?? "", b.ChildDateOfBirth);
        if (invalid is not null) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await db.DetailAsync(application.Id, ct, c.AsAdmin))!;
    }
}
