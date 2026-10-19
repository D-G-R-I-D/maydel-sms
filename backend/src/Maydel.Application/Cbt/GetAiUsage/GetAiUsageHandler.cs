using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;
using Maydel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.GetAiUsage;

internal sealed class GetAiUsageHandler(IAppDbContext db, AiQuestions ai, ISchoolClock clock) : IQueryHandler<GetAiUsageQuery, AiUsage>
{
    public async Task<Result<AiUsage>> Handle(GetAiUsageQuery q, CancellationToken ct)
    {
        var at = q is { Year: { } y, Month: { } m } && m is >= 1 and <= 12 && y is >= 2000 and <= 9999
            ? new DateTimeOffset(y, m, 15, 12, 0, 0, TimeSpan.Zero)
            : clock.Now;
        var (from, to) = AiQuota.Month(at);
        var settings = await ai.SettingsAsync(ct);
        var used = await db.AiGenerations.AsNoTracking()
            .SelectMany(g => g.Versions.Where(v => v.CreatedAt >= from && v.CreatedAt < to), (g, v) => new { g.UserId, v.Status, v.InputTokens, v.OutputTokens })
            .ToListAsync(ct);
        var custom = await db.AiAllowances.AsNoTracking().ToDictionaryAsync(a => a.UserId, a => a.MonthlyGenerations, ct);

        // Every teacher and admin appears, even with no use, so allowances can be set before anyone starts.
        var staffRoles = await db.Roles.Where(r => r.Name == Roles.Teacher || r.Name == Roles.Admin).Select(r => r.Id).ToListAsync(ct);
        var staffIds = await db.UserRoles.Where(ur => staffRoles.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToListAsync(ct);
        var ids = staffIds.Union(used.Select(u => u.UserId)).ToList();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var rows = names.Select(n =>
        {
            var mine = used.Where(u => u.UserId == n.Key).ToList();
            return new AiUsageRow(n.Key, n.Value, custom.GetValueOrDefault(n.Key, settings.DefaultMonthlyGenerations), custom.ContainsKey(n.Key),
                mine.Count(u => u.Status == GenerationStatus.Ready), mine.Sum(u => u.InputTokens), mine.Sum(u => u.OutputTokens));
        }).OrderByDescending(r => r.Used).ThenBy(r => r.Name).ToList();
        return new AiUsage(from, to, settings.SchoolMonthlyGenerations, used.Count(u => u.Status == GenerationStatus.Ready),
            used.Sum(u => u.InputTokens), used.Sum(u => u.OutputTokens), rows);
    }
}

internal sealed class GetAiSettingsHandler(AiQuestions ai, IQuestionGenerator generator) : IQueryHandler<GetAiSettingsQuery, AiSettingsDto>
{
    public async Task<Result<AiSettingsDto>> Handle(GetAiSettingsQuery q, CancellationToken ct)
    {
        var s = await ai.SettingsAsync(ct);
        return new AiSettingsDto(generator.IsEnabled, s.Enabled, s.DefaultMonthlyGenerations, s.SchoolMonthlyGenerations);
    }
}
