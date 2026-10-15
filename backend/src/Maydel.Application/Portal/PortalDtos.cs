using Maydel.Application.Abstractions;
using Maydel.Domain.Academics;
using Microsoft.EntityFrameworkCore;
using Maydel.Domain.Assessment;

namespace Maydel.Application.Portal;

/// <summary>A published term result in the portal list (E6.2). Drafts never appear.</summary>
public sealed record PortalResultSummary(
    Guid TermId, TermNumber Number, string Term, string Session, string ClassRoom,
    decimal Average, int? Position, int ClassSize, DateTimeOffset? PublishedAt);

/// <summary>A child the signed-in parent may see, or the signed-in student themselves (E6.1 children switcher).</summary>
public sealed record PortalChild(
    Guid StudentId, string Name, string AdmissionNumber, string? ClassRoom, string? PhotoVersion, PortalResultSummary? LatestResult);

/// <summary>A child's fees (E6.3): live invoices newest first, with what's owed and overdue across them.</summary>
public sealed record PortalFees(Guid StudentId, decimal Balance, decimal Overdue, IReadOnlyList<Finance.InvoiceDetail> Invoices);

public static class PortalErrors
{
    public static readonly Common.Error InvoiceNotFound = Common.Error.NotFound("invoice.not_found", "We couldn't find that invoice for this child.");

    /// <summary>Not linked to this account looks the same as not existing, so other families' children can't be probed.</summary>
    public static readonly Common.Error StudentNotFound = Common.Error.NotFound("portal.student_not_found", "We couldn't find that child on your account.");
    public static readonly Common.Error NotPublished = Common.Error.NotFound("report.not_published", "That result hasn't been published yet.");
}

internal sealed record PublishedRow(Guid StudentId, PortalResultSummary Result);

internal static class PortalQueries
{
    /// <summary>Published results of these students, newest first.</summary>
    public static IQueryable<PublishedRow> PublishedResults(this IAppDbContext db, IReadOnlyCollection<Guid> studentIds) =>
        db.TermReports.AsNoTracking()
            .Where(r => studentIds.Contains(r.StudentId) && r.Status == ReportStatus.Published)
            .OrderByDescending(r => r.Term.Session.StartsOn).ThenByDescending(r => r.Term.Number)
            .Select(r => new PublishedRow(r.StudentId, new PortalResultSummary(r.TermId, r.Term.Number, r.Term.Number.ToString() + " Term", r.Term.Session.Name,
                r.ClassRoom.ClassLevel.Name + " " + r.ClassRoom.Arm, r.Average, r.Position, r.ClassSize, r.PublishedAt)));
}
