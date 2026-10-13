using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Assessment.SaveAssessmentSettings;

internal sealed class SaveAssessmentSettingsHandler(IAppDbContext db) : ICommandHandler<SaveAssessmentSettingsCommand, AssessmentSettings>
{
    public async Task<Result<AssessmentSettings>> Handle(SaveAssessmentSettingsCommand c, CancellationToken ct)
    {
        if (AssessmentRules.CheckComponents([.. c.Components.Select(x => new AssessmentRules.ComponentInput(x.Name, x.MaxScore, x.IsActive))]) is { } badComponents)
            return badComponents.ToError();
        if (AssessmentRules.CheckBands([.. c.Bands.Select(b => new AssessmentRules.BandInput(b.Grade, b.MinScore, b.MaxScore, b.Remark))]) is { } badBands)
            return badBands.ToError();

        var existing = await db.AssessmentComponents.ToListAsync(ct);
        if (c.Components.Any(x => x.Id is { } id && existing.All(e => e.Id != id)))
            return Error.Validation("components", "One of the components no longer exists. Reload and try again.");

        // Highest score already entered per component: a maximum can't drop below it.
        var highest = await db.SubjectScores.SelectMany(s => s.Components)
            .GroupBy(x => x.ComponentId).Select(g => new { g.Key, Max = g.Max(x => x.Score) })
            .ToDictionaryAsync(x => x.Key, x => x.Max, ct);

        var order = 0;
        foreach (var input in c.Components)
        {
            var component = input.Id is { } id ? existing.Single(e => e.Id == id) : null;
            if (component is not null && highest.TryGetValue(component.Id, out var max) && input.MaxScore < max)
                return Error.Rule("assessment.max_below_scores", $"{component.Name} already has a score of {max:0.##}; its maximum can't be lower than that.");
            if (component is null) db.AssessmentComponents.Add(component = new AssessmentComponent { Name = input.Name.Trim() });
            (component.Name, component.MaxScore, component.IsActive, component.Order) = (input.Name.Trim(), input.MaxScore, input.IsActive, ++order);
        }

        // Left out of the list: keep (inactive) if it has scores so history stays intact, otherwise delete.
        foreach (var removed in existing.Where(e => c.Components.All(i => i.Id != e.Id)))
        {
            if (highest.ContainsKey(removed.Id)) (removed.IsActive, removed.Order) = (false, ++order);
            else db.AssessmentComponents.Remove(removed);
        }

        db.GradeBands.RemoveRange(await db.GradeBands.ToListAsync(ct));
        db.GradeBands.AddRange(c.Bands.Select(b => new GradeBand { Grade = b.Grade.Trim(), MinScore = b.MinScore, MaxScore = b.MaxScore, Remark = b.Remark.Trim() }));

        await db.SaveChangesAsync(ct);
        return await db.GetSettingsAsync(ct);
    }
}
