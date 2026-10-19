using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;
using Maydel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.SaveAiSettings;

internal sealed class SaveAiSettingsHandler(IAppDbContext db, IQuestionGenerator generator) : ICommandHandler<SaveAiSettingsCommand, AiSettingsDto>
{
    public async Task<Result<AiSettingsDto>> Handle(SaveAiSettingsCommand c, CancellationToken ct)
    {
        if (AiSettings.Check(c.DefaultMonthlyGenerations, c.SchoolMonthlyGenerations) is { } invalid) return invalid.ToError();
        var s = await db.AiSettings.FirstOrDefaultAsync(ct);
        if (s is null) db.AiSettings.Add(s = new AiSettings());
        (s.Enabled, s.DefaultMonthlyGenerations, s.SchoolMonthlyGenerations) = (c.Enabled, c.DefaultMonthlyGenerations, c.SchoolMonthlyGenerations);
        await db.SaveChangesAsync(ct);
        return new AiSettingsDto(generator.IsEnabled, s.Enabled, s.DefaultMonthlyGenerations, s.SchoolMonthlyGenerations);
    }
}

internal sealed class SetAiAllowanceHandler(IAppDbContext db, AiQuestions ai) : ICommandHandler<SetAiAllowanceCommand, AiUsageRow>
{
    public async Task<Result<AiUsageRow>> Handle(SetAiAllowanceCommand c, CancellationToken ct)
    {
        var user = await db.Users.Where(u => u.Id == c.UserId).Select(u => new { u.Id, u.FullName }).FirstOrDefaultAsync(ct);
        if (user is null) return Error.NotFound("user.not_found", "That user doesn't exist.");
        if (c.MonthlyGenerations is < 0 or > 1000) return Error.Validation("monthlyGenerations", "The allowance must be between 0 and 1000 a month.");
        var row = await db.AiAllowances.FirstOrDefaultAsync(a => a.UserId == c.UserId, ct);
        if (c.MonthlyGenerations is { } monthly)
        {
            if (row is null) db.AiAllowances.Add(row = new AiAllowance { UserId = c.UserId });
            row.MonthlyGenerations = monthly;
        }
        else if (row is not null)
        {
            db.AiAllowances.Remove(row);
        }
        await db.SaveChangesAsync(ct);
        var quota = await ai.QuotaAsync(c.UserId, ct);
        return new AiUsageRow(user.Id, user.FullName, quota.Allowance, c.MonthlyGenerations is not null, quota.Used, 0, 0);
    }
}
