using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Notifications.ListReportEmails;

internal sealed class ListReportEmailsHandler(IAppDbContext db) : IQueryHandler<ListReportEmailsQuery, IReadOnlyList<ReportEmailStatus>>
{
    public async Task<Result<IReadOnlyList<ReportEmailStatus>>> Handle(ListReportEmailsQuery q, CancellationToken ct)
    {
        var students = db.TermReports.Where(r => r.ClassRoomId == q.ClassRoomId && r.TermId == q.TermId).Select(r => r.StudentId);
        return await db.OutboxEmails.AsNoTracking()
            .Where(e => e.Kind == EmailKind.ReportPublished && e.TermId == q.TermId && e.StudentId != null && students.Contains(e.StudentId.Value))
            .OrderByDescending(e => e.CreatedAt)
            .Join(db.Students, e => e.StudentId, s => s.Id, (e, s) => new ReportEmailStatus(
                e.Id, s.Id, s.FirstName + " " + s.LastName, e.ToName ?? e.ToAddress, e.ToAddress, e.Status, e.Attempts,
                e.LastError, e.CreatedAt, e.SentAt))
            .ToListAsync(ct);
    }
}
