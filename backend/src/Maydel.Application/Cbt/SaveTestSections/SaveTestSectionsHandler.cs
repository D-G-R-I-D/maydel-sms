using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.SaveTestSections;

internal sealed class SaveTestSectionsHandler(IAppDbContext db, Tests tests) : ICommandHandler<SaveTestSectionsCommand, TestDetail>
{
    public async Task<Result<TestDetail>> Handle(SaveTestSectionsCommand c, CancellationToken ct)
    {
        if (await tests.ManageableAsync(c.TestId, ct) is not { } test) return CbtErrors.TestNotFound;
        if (test.CanEdit() is { } locked) return locked.ToError();
        if (c.Sections.Any(s => !Enum.IsDefined(s.Kind) || !Enum.IsDefined(s.Mode)))
            return Error.Validation("sections", "Each section needs a question kind and how its questions are chosen.");

        var sections = c.Sections.Select(s => new TestSection
        {
            Title = s.Title ?? "", Instructions = string.IsNullOrWhiteSpace(s.Instructions) ? null : s.Instructions.Trim(), Kind = s.Kind,
            MarksPerQuestion = s.MarksPerQuestion, Mode = s.Mode, QuestionIds = [.. s.QuestionIds ?? []], RandomCount = s.RandomCount, Topic = s.Topic,
            Difficulty = s.Difficulty,
        }).ToList();
        if (sections.Any(s => s.Instructions is { Length: > 1000 })) return Error.Validation("sections", "Keep section instructions under 1000 characters.");
        test.Sections = sections;
        if (test.CheckSections() is { } invalid) return invalid.ToError();

        // Fixed questions must exist in this subject and level's bank and match the section's kind.
        var ids = sections.SelectMany(s => s.QuestionIds).ToList();
        var bank = await db.Questions.AsNoTracking()
            .Where(q => ids.Contains(q.Id) && q.SubjectId == test.SubjectId && q.ClassLevelId == test.ClassLevelId && !q.IsArchived)
            .ToDictionaryAsync(q => q.Id, q => q.Kind, ct);
        foreach (var s in sections)
            if (s.QuestionIds.Any(id => !bank.TryGetValue(id, out var kind) || kind != s.Kind))
                return Error.Validation("sections", $"Section {s.Order + 1} ({s.Title}) can only hold {s.Kind} questions from this subject's bank.");

        await db.SaveChangesAsync(ct);
        return await tests.DetailAsync(test.Id, ct);
    }
}
