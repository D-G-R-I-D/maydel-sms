using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.MarkExamAbsent;

internal sealed class MarkExamAbsentHandler(IAppDbContext db, ISchoolClock clock, AdmissionEmails emails) : ICommandHandler<MarkExamAbsentCommand, ExamCandidate>
{
    public async Task<Result<ExamCandidate>> Handle(MarkExamAbsentCommand c, CancellationToken ct)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == c.ApplicationId, ct);
        if (application is null) return AdmissionErrors.ApplicationNotFound;
        if (application.MarkAbsent(clock.Now.ToUniversalTime()) is { } invalid) return invalid.ToError();
        await emails.QueueAsync(application, "Entrance exam missed",
            $"{application.ChildName} didn't sit the entrance exam as booked. If you'd still like to continue, please contact the school to reopen the application.", ct);
        await db.SaveChangesAsync(ct);
        return await db.Candidates(db.Applications.AsNoTracking().Where(a => a.Id == application.Id)).FirstAsync(ct);
    }
}
