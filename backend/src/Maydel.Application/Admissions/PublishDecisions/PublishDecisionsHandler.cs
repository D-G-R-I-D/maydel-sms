using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Students;
using Maydel.Domain.Admissions;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Admissions.PublishDecisions;

internal sealed class PublishDecisionsHandler(IAppDbContext db, ISchoolClock clock, AdmissionEmails emails, IOptions<EmailOptions> options)
    : ICommandHandler<PublishDecisionsCommand, PublishDecisionsResult>
{
    public async Task<Result<PublishDecisionsResult>> Handle(PublishDecisionsCommand c, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().Where(s => s.Id == c.SessionId).Select(s => new { s.Name, s.StartsOn }).FirstOrDefaultAsync(ct);
        if (session is null) return AdmissionErrors.SessionNotFound;
        var applications = await db.Applications.Where(a => a.SessionId == c.SessionId && a.Status == ApplicationStatus.Assessed)
            .OrderBy(a => a.ChildLastName).ThenBy(a => a.ChildFirstName).ToListAsync(ct);
        if (applications.Count == 0) return Error.Rule("admission.nothing_to_publish", "There are no scored applicants waiting for a decision.");

        var levels = await db.ClassLevels.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var now = clock.Now.ToUniversalTime();
        var portal = options.Value.PortalUrl is { } url ? $"{url.TrimEnd('/')}/admissions" : "your admissions page";

        // Numbers are allocated in one go (MSS/{session start year}/####), continuing after students and earlier offers.
        var next = await db.NextAdmissionNumberAsync(session.StartsOn.Year, ct);
        var prefix = Domain.People.Student.AdmissionPrefix(session.StartsOn.Year);
        var sequence = int.Parse(next[prefix.Length..]);

        int offered = 0, declined = 0;
        foreach (var application in applications)
        {
            var offer = application.Decision == OfferDecision.Offer;
            application.PublishDecision(now, offer ? Domain.People.Student.AdmissionNumberFor(session.StartsOn.Year, sequence++) : null);
            if (offer)
            {
                offered++;
                await emails.QueueAsync(application, "Offer of admission",
                    $"Congratulations! We are delighted to offer {application.ChildName} admission into {levels[application.ClassLevelId]} for the {session.Name} session. " +
                    $"{application.ChildName}'s admission number is {application.AdmissionNumber}. The admission letter is attached.\n\n" +
                    "What happens next:\n" +
                    $"1. Accept the offer on {portal}.\n" +
                    "2. The school completes enrolment. You then sign in with this same email and password to see the parent portal.\n" +
                    "3. In the parent portal you can view and pay school fees online (new-intake items such as uniform and books are on the first invoice), " +
                    "see report cards and results, and read school notices.\n\n" +
                    "We look forward to welcoming you to the school family.", ct, EmailKind.AdmissionOffer);
            }
            else
            {
                declined++;
                await emails.QueueAsync(application, "Your application",
                    $"Thank you for applying to our school and for the effort {application.ChildName} put into the entrance exam. " +
                    "After careful consideration, we are unable to offer a place this time. " +
                    $"We wish {application.ChildName} every success, and you are welcome to apply again in future.", ct);
            }
        }
        await db.SaveChangesAsync(ct);
        return new PublishDecisionsResult(offered, declined);
    }
}
