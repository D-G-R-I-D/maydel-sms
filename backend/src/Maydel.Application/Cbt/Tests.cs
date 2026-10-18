using Maydel.Application.Abstractions;
using Maydel.Application.Common.Access;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt;

/// <summary>Shared test logic: access, readiness (what blocks publishing) and the detail view.</summary>
public sealed class Tests(IAppDbContext db, AccessRules access)
{
    /// <summary>The test if the caller may manage it (author access to its subject and level), else null.</summary>
    public async Task<CbtTest?> ManageableAsync(Guid id, CancellationToken ct, bool track = true)
    {
        var tests = track ? db.CbtTests : db.CbtTests.AsNoTracking();
        var test = await tests.FirstOrDefaultAsync(t => t.Id == id, ct);
        return test is not null && await access.CanAuthorAsync(test.ClassLevelId, test.SubjectId, ct) ? test : null;
    }

    /// <summary>Checks the header's references and that the caller may set the test for each chosen arm.</summary>
    public async Task<Common.Error?> CheckHeaderAsync(TestInput input, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == input.SubjectId, ct) || !await db.ClassLevels.AnyAsync(l => l.Id == input.ClassLevelId, ct))
            return CbtErrors.SubjectOrLevel;
        if (!await access.CanAuthorAsync(input.ClassLevelId, input.SubjectId, ct)) return CbtErrors.NotYourSubject;
        if (!await db.Terms.AnyAsync(t => t.Id == input.TermId, ct)) return CbtErrors.TermNotFound;
        if (input.ComponentId is { } component && !await db.AssessmentComponents.AnyAsync(c => c.Id == component && c.IsActive, ct))
            return CbtErrors.ComponentNotFound;

        var rooms = (input.ClassRoomIds ?? []).Distinct().ToList();
        if (rooms.Count == 0) return Common.Error.Validation("classRoomIds", "Choose at least one class to sit the test.");
        var offered = await db.ClassSubjects.Where(cs => rooms.Contains(cs.ClassRoomId) && cs.SubjectId == input.SubjectId && cs.ClassRoom.ClassLevelId == input.ClassLevelId)
            .Select(cs => cs.ClassRoomId).ToListAsync(ct);
        if (offered.Count != rooms.Count) return Common.Error.Validation("classRoomIds", "Choose classes at this level that take this subject.");
        foreach (var room in rooms)
            if (!await access.CanGradeAsync(room, input.SubjectId, ct))
                return Common.Error.Validation("classRoomIds", "You can only set tests for classes you teach this subject in.");
        return null;
    }

    /// <summary>Everything that stops the test being published, in plain words.</summary>
    public async Task<IReadOnlyList<string>> ProblemsAsync(CbtTest test, CancellationToken ct)
    {
        var problems = new List<string>();
        if (test.CheckSchedule() is { } schedule) problems.Add(schedule.Message);
        if (test.Sections.Count == 0) problems.Add("Add at least one section.");
        if (test.CheckSections() is { } sections) problems.Add(sections.Message);

        var fixedIds = test.Sections.SelectMany(s => s.QuestionIds).ToList();
        var live = await db.Questions.AsNoTracking().Where(q => fixedIds.Contains(q.Id))
            .Select(q => new { q.Id, q.Kind, q.SubjectId, q.ClassLevelId, q.IsArchived }).ToDictionaryAsync(q => q.Id, ct);
        foreach (var s in test.Sections)
        {
            var label = $"Section {s.Order + 1} ({s.Title})";
            if (s.Mode == SectionMode.Fixed)
            {
                if (s.QuestionIds.Count == 0) problems.Add($"{label} has no questions.");
                foreach (var id in s.QuestionIds)
                {
                    if (!live.TryGetValue(id, out var q) || q.SubjectId != test.SubjectId || q.ClassLevelId != test.ClassLevelId)
                        problems.Add($"{label} has a question from another subject or class, or one that no longer exists.");
                    else if (q.IsArchived) problems.Add($"{label} has a question that was removed from the bank.");
                    else if (q.Kind != s.Kind) problems.Add($"{label} is for {s.Kind} questions but contains a {q.Kind} question.");
                    else continue;
                    break;
                }
            }
            else if (await AvailableAsync(test, s, ct) is var available && available < (s.RandomCount ?? 0))
            {
                problems.Add($"{label} draws {s.RandomCount} questions but the bank only has {available} that match.");
            }
        }
        return problems;
    }

    public Task<int> AvailableAsync(CbtTest test, TestSection s, CancellationToken ct)
    {
        var bank = db.Questions.Where(q => q.SubjectId == test.SubjectId && q.ClassLevelId == test.ClassLevelId && !q.IsArchived && q.Kind == s.Kind);
        if (s.Topic is { } topic) bank = bank.Where(q => q.Topic == topic);
        if (s.Difficulty is { } difficulty) bank = bank.Where(q => q.Difficulty == difficulty);
        return bank.CountAsync(ct);
    }

    public async Task<TestDetail> DetailAsync(Guid id, CancellationToken ct)
    {
        var test = await db.CbtTests.AsNoTracking().FirstAsync(t => t.Id == id, ct);
        var subject = await db.Subjects.Where(s => s.Id == test.SubjectId).Select(s => s.Name).FirstAsync(ct);
        var level = await db.ClassLevels.Where(l => l.Id == test.ClassLevelId).Select(l => l.Name).FirstAsync(ct);
        var term = await db.Terms.Include(t => t.Session).Where(t => t.Id == test.TermId).FirstAsync(ct);
        var rooms = await db.ClassRooms.AsNoTracking().Where(r => test.ClassRoomIds.Contains(r.Id)).OrderBy(r => r.Arm)
            .Select(r => new ClassRoomBrief(r.Id, r.ClassLevel.Name + " " + r.Arm)).ToListAsync(ct);
        var ids = test.Sections.SelectMany(s => s.QuestionIds).ToList();
        var briefs = await db.Questions.AsNoTracking().Where(q => ids.Contains(q.Id))
            .Select(q => new QuestionBrief(q.Id, q.Kind, q.Text.Length > 200 ? q.Text.Substring(0, 200) + "…" : q.Text, q.Topic, q.Difficulty, q.ImagePath != null, q.IsArchived))
            .ToDictionaryAsync(q => q.Id, ct);
        var sections = new List<SectionDto>();
        foreach (var s in test.Sections.OrderBy(s => s.Order))
            sections.Add(new SectionDto(s.Id, s.Order, s.Title, s.Instructions, s.Kind, s.MarksPerQuestion, s.Mode, s.QuestionCount, s.Marks, s.RandomCount,
                s.Topic, s.Difficulty, [.. s.QuestionIds.Where(briefs.ContainsKey).Select(q => briefs[q])],
                s.Mode == SectionMode.Random ? await AvailableAsync(test, s, ct) : null));

        ScalingPreview? scaling = null;
        if (test.ComponentId is { } componentId
            && await db.AssessmentComponents.AsNoTracking().FirstOrDefaultAsync(c => c.Id == componentId, ct) is { } component)
            scaling = new ScalingPreview(component.Id, component.Name, component.MaxScore, test.TotalMarks,
                test.TotalMarks <= 0 ? 0 : Math.Round(component.MaxScore / test.TotalMarks, 4));

        return new TestDetail(test.Id, test.Title, test.Instructions, test.SubjectId, subject, test.ClassLevelId, level, test.TermId, term.DisplayName,
            test.ComponentId, rooms, test.DurationMinutes, test.OpensAt, test.ClosesAt, test.MaxAttempts, test.ShuffleQuestions, test.ShuffleOptions,
            test.Status, test.PublishedAt, sections, test.QuestionCount, test.TotalMarks, scaling,
            test.Status == TestStatus.Draft ? await ProblemsAsync(test, ct) : [], test.Status == TestStatus.Draft);
    }

    public static void ApplyHeader(CbtTest test, TestInput input)
    {
        (test.Title, test.Instructions, test.SubjectId, test.ClassLevelId, test.TermId, test.ComponentId) =
            ((input.Title ?? "").Trim(), string.IsNullOrWhiteSpace(input.Instructions) ? null : input.Instructions.Trim(), input.SubjectId, input.ClassLevelId,
                input.TermId, input.ComponentId);
        test.ClassRoomIds = [.. (input.ClassRoomIds ?? []).Distinct()];
        (test.DurationMinutes, test.OpensAt, test.ClosesAt, test.MaxAttempts, test.ShuffleQuestions, test.ShuffleOptions) =
            (input.DurationMinutes, input.OpensAt.ToUniversalTime(), input.ClosesAt.ToUniversalTime(), input.MaxAttempts, input.ShuffleQuestions, input.ShuffleOptions);
    }
}
