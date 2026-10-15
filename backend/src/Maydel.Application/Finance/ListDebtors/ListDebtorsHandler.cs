using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.ListDebtors;

internal sealed class ListDebtorsHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<ListDebtorsQuery, DebtorsReport>
{
    public async Task<Result<DebtorsReport>> Handle(ListDebtorsQuery q, CancellationToken ct)
    {
        var term = await db.Terms.AsNoTracking().Include(t => t.Session).FirstOrDefaultAsync(t => t.Id == q.TermId, ct);
        if (term is null) return FinanceErrors.TermNotFound;

        var invoices = await db.Invoices.AsNoTracking().Include(i => i.Payments).Include(i => i.Student)
            .Where(i => i.TermId == term.Id && !i.IsCancelled && (q.ClassRoomId == null || i.ClassRoomId == q.ClassRoomId)).ToListAsync(ct);
        var owing = invoices.Where(i => i.Balance > 0).ToList();
        var studentIds = owing.Select(i => i.StudentId).ToList();
        var rooms = await db.ClassRooms.AsNoTracking().Select(r => new { r.Id, Name = r.ClassLevel.Name + " " + r.Arm }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var guardians = (await db.StudentGuardians.AsNoTracking().Where(g => studentIds.Contains(g.StudentId))
                .OrderByDescending(g => g.IsPrimaryContact).Select(g => new { g.StudentId, g.Guardian.FullName, g.Guardian.Phone }).ToListAsync(ct))
            .GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.First());
        var plan = await db.PlanForAsync(term, ct) is { Plan.Enabled: true } p ? p.Plan.Instalments.OrderBy(x => x.Order).ToList() : [];
        var today = clock.Today;

        var rows = owing.Select(i =>
        {
            var amounts = plan.Count == 0 ? [] : FeeRules.Split(i.Total, [.. plan.Select(x => x.Percent)]);
            var schedule = plan.Select((x, n) => (amounts[n], x.DueOn)).ToList();
            var dueNow = FeeRules.DueBy(today, i.Total, i.DueOn, schedule);
            var next = schedule.Count > 0 ? schedule.Where(s => s.DueOn > today).Select(s => (DateOnly?)s.DueOn).FirstOrDefault()
                : i.DueOn > today ? i.DueOn : null;
            guardians.TryGetValue(i.StudentId, out var g);
            return new DebtorRow(i.StudentId, i.Student.AdmissionNumber, $"{i.Student.FirstName} {i.Student.LastName}",
                i.ClassRoomId is { } r ? rooms.GetValueOrDefault(r) : null, i.Id, i.Number, i.Total, i.AmountPaid, i.Balance,
                dueNow, Math.Max(0, dueNow - i.AmountPaid), next, g?.FullName, g?.Phone);
        }).Where(r => !q.OverdueOnly || r.Overdue > 0).OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Balance).ToList();

        return new DebtorsReport(term.Id, $"{term.Number} Term {term.Session.Name}", rows.Count, rows.Sum(r => r.Balance), rows.Sum(r => r.Overdue), rows);
    }
}
