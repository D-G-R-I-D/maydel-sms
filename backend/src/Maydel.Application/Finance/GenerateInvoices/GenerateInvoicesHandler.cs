using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.GenerateInvoices;

internal sealed class GenerateInvoicesHandler(IAppDbContext db) : ICommandHandler<GenerateInvoicesCommand, GenerateInvoicesResult>
{
    public async Task<Result<GenerateInvoicesResult>> Handle(GenerateInvoicesCommand c, CancellationToken ct)
    {
        var term = await db.Terms.AsNoTracking().Include(t => t.Session).FirstOrDefaultAsync(t => t.Id == c.TermId, ct);
        if (term is null) return FinanceErrors.TermNotFound;
        if (c.ClassLevelId is { } level && !await db.ClassLevels.AnyAsync(l => l.Id == level, ct)) return FinanceErrors.ClassLevelNotFound;

        var items = await db.FeeItems.AsNoTracking().Include(f => f.OptIns).Where(f => f.TermId == term.Id).ToListAsync(ct);
        var students = await db.Students.AsNoTracking()
            .Where(s => s.Status == StudentStatus.Active && s.ClassRoomId != null && (c.ClassLevelId == null || s.ClassRoom!.ClassLevelId == c.ClassLevelId))
            .Select(s => new { s.Id, s.ClassRoomId, LevelId = s.ClassRoom!.ClassLevelId, s.AdmittedOn }).ToListAsync(ct);
        var invoiced = (await db.Invoices.Where(i => i.TermId == term.Id && !i.IsCancelled).Select(i => i.StudentId).ToListAsync(ct)).ToHashSet();

        var settings = await db.SettingsAsync(ct);
        var ranks = settings.SiblingMode == SiblingDiscountMode.None ? new Dictionary<Guid, int>() : await SiblingRanksAsync(ct);
        var plan = await db.PlanForAsync(term, ct);
        var dueOn = plan is { Plan.Enabled: true } p ? p.Plan.Instalments.OrderBy(i => i.Order).First().DueOn : term.StartsOn;

        // Next number in this session's sequence. The unique index on Number guards against a concurrent run.
        var prefix = FeeRules.InvoiceNumber(term.Session.Name, 0)[..^4];
        var used = await db.Invoices.Where(i => i.Number.StartsWith(prefix)).Select(i => i.Number).ToListAsync(ct);
        var next = used.Count == 0 ? 1 : used.Max(n => int.Parse(n[prefix.Length..])) + 1;

        int created = 0, already = 0, noFees = 0;
        decimal billed = 0;
        foreach (var s in students)
        {
            if (invoiced.Contains(s.Id)) { already++; continue; }
            var newIntake = FeeRules.IsNewIntake(s.AdmittedOn, term.StartsOn, term.EndsOn);
            var mine = items.Where(f => (f.ClassLevelId == null || f.ClassLevelId == s.LevelId) && FeeRules.Applies(f.Audience, newIntake)
                && f.Charges(s.Id)).ToList();
            if (mine.Count == 0) { noFees++; continue; }

            var lines = mine.Select(f => new InvoiceLine { Description = f.Name, Amount = f.Amount }).ToList();
            var rank = ranks.GetValueOrDefault(s.Id, 1);
            var discount = FeeRules.SiblingDiscount(settings.SiblingMode, settings.SecondChild, settings.ThirdAndLater, rank,
                mine.Where(f => f.SiblingDiscount).Sum(f => f.Amount));
            if (discount > 0) lines.Add(new InvoiceLine { Description = $"Sibling discount ({Ordinal(rank)} child)", Amount = -discount });

            db.Invoices.Add(new Invoice
            {
                Number = FeeRules.InvoiceNumber(term.Session.Name, next++), StudentId = s.Id, ClassRoomId = s.ClassRoomId, TermId = term.Id,
                DueOn = dueOn, Lines = lines, NewIntake = newIntake,
            });
            created++;
            billed += lines.Sum(l => l.Amount);
        }
        await db.SaveChangesAsync(ct);
        return new GenerateInvoicesResult(created, already, noFees, billed);
    }

    /// <summary>Families = active students linked through any shared guardian; each child's place in the family by age.</summary>
    private async Task<Dictionary<Guid, int>> SiblingRanksAsync(CancellationToken ct)
    {
        var links = await db.StudentGuardians.AsNoTracking().Where(g => g.Student.Status == StudentStatus.Active)
            .Select(g => new { g.StudentId, g.GuardianId }).ToListAsync(ct);
        var parent = new Dictionary<Guid, Guid>();
        Guid Find(Guid x) { while (parent.TryGetValue(x, out var p) && p != x) x = parent[x] = parent.GetValueOrDefault(p, p); return x; }
        foreach (var l in links)
        {
            parent.TryAdd(l.StudentId, l.StudentId);
            parent.TryAdd(l.GuardianId, l.GuardianId);
            parent[Find(l.StudentId)] = Find(l.GuardianId);
        }
        var studentIds = links.Select(l => l.StudentId).Distinct().ToList();
        var children = await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.DateOfBirth, s.AdmittedOn }).ToListAsync(ct);
        return children.GroupBy(s => Find(s.Id))
            .SelectMany(family => FeeRules.SiblingRanks(family.Select(s => (s.Id, s.DateOfBirth, s.AdmittedOn))))
            .ToDictionary(x => x.Key, x => x.Value);
    }

    private static string Ordinal(int n) => n switch { 2 => "2nd", 3 => "3rd", _ => $"{n}th" };
}
