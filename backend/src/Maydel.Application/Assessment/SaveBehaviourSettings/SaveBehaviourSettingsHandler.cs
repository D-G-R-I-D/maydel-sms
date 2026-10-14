using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Assessment.SaveBehaviourSettings;

internal sealed class SaveBehaviourSettingsHandler(IAppDbContext db) : ICommandHandler<SaveBehaviourSettingsCommand, BehaviourSettings>
{
    public async Task<Result<BehaviourSettings>> Handle(SaveBehaviourSettingsCommand c, CancellationToken ct)
    {
        var existing = await db.BehaviourTraits.ToListAsync(ct);
        if (c.Traits.Any(x => x.Id is { } id && existing.All(e => e.Id != id)))
            return Error.Validation("traits", "One of the traits no longer exists. Reload and try again.");
        var used = await db.TermReports.SelectMany(r => r.TraitRatings).Select(r => r.TraitId).Distinct().ToListAsync(ct);

        var order = 0;
        foreach (var input in c.Traits)
        {
            var trait = input.Id is { } id ? existing.Single(e => e.Id == id) : null;
            if (trait is null) db.BehaviourTraits.Add(trait = new BehaviourTrait { Name = input.Name.Trim() });
            (trait.Name, trait.IsActive, trait.Order) = (input.Name.Trim(), input.IsActive, ++order);
        }

        // Left out of the list: keep (inactive) if a report rates it so history stays intact, otherwise delete.
        foreach (var removed in existing.Where(e => c.Traits.All(i => i.Id != e.Id)))
        {
            if (used.Contains(removed.Id)) (removed.IsActive, removed.Order) = (false, ++order);
            else db.BehaviourTraits.Remove(removed);
        }

        await db.SaveChangesAsync(ct);
        return await db.GetBehaviourSettingsAsync(ct);
    }
}
