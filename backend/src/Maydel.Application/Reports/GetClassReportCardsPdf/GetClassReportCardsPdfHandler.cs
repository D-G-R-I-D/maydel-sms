using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports.GetClassReportCardsPdf;

internal sealed class GetClassReportCardsPdfHandler(IAppDbContext db, AccessRules access, ReportBuilder reports, ReportPdfs pdfs)
    : IQueryHandler<GetClassReportCardsPdfQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetClassReportCardsPdfQuery q, CancellationToken ct)
    {
        if (!await access.TeachesClassAsync(q.ClassRoomId, ct)) return ReportErrors.NotAllowed;
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == q.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        if (room is null) return ReportErrors.ClassNotFound;
        var term = await db.Terms.AsNoTracking().Where(t => t.Id == q.TermId).Select(t => t.Number.ToString() + " Term " + t.Session.Name).FirstOrDefaultAsync(ct);
        if (term is null) return ReportErrors.TermNotFound;

        // Students who have a result this term, in register order.
        var figures = await reports.ComputeAsync(q.ClassRoomId, q.TermId, ct);
        var ids = await db.Students.AsNoTracking()
            .Where(s => s.ClassRoomId == q.ClassRoomId && s.Status == StudentStatus.Active && figures.Averages.Keys.Contains(s.Id))
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).Select(s => s.Id).ToListAsync(ct);
        if (ids.Count == 0) return ReportErrors.NothingToPublish;

        var cards = new List<ReportCard>(ids.Count);
        foreach (var id in ids) cards.Add((await reports.BuildAsync(id, q.TermId, q.ClassRoomId, ct))!);
        return new PdfFile(await pdfs.RenderAsync(cards, ct), ReportPdfs.Safe($"Report cards - {room} - {term}") + ".pdf");
    }
}
