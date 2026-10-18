using Maydel.Application.Abstractions;
using Maydel.Domain.Cbt;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt;

public sealed record OptionDto(Guid Id, string Text, bool IsCorrect);

/// <summary>A bank question with its answer key (staff only; students never see keys).</summary>
public sealed record QuestionDto(
    Guid Id, Guid SubjectId, string Subject, Guid ClassLevelId, string ClassLevel, QuestionKind Kind, string Text, bool HasImage,
    IReadOnlyList<OptionDto> Options, bool? TrueFalseAnswer, IReadOnlyList<string> AcceptedAnswers, string? MarkingGuide, string? Topic,
    Difficulty Difficulty, QuestionSource Source, bool IsAutoMarked, string? CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>An option as the teacher writes it; <c>Id</c> keeps an existing option (so answers already given still match).</summary>
public sealed record OptionInput(Guid? Id, string Text, bool IsCorrect);

public sealed record QuestionInput(
    Guid SubjectId, Guid ClassLevelId, QuestionKind Kind, string Text, IReadOnlyList<OptionInput>? Options, bool? TrueFalseAnswer,
    IReadOnlyList<string>? AcceptedAnswers, string? MarkingGuide, string? Topic, Difficulty Difficulty = Difficulty.Medium);

/// <summary>A subject at a class level the caller may write questions for, with how many live questions the bank has.</summary>
public sealed record AuthoringScope(Guid SubjectId, string Subject, Guid ClassLevelId, string ClassLevel, int Questions);

public static class CbtErrors
{
    public static readonly Common.Error QuestionNotFound = Common.Error.NotFound("question.not_found", "That question doesn't exist.");
    public static readonly Common.Error NotYourSubject = Common.Error.Forbidden("question.forbidden",
        "You can only write questions for subjects and classes you teach.");
    public static readonly Common.Error SubjectOrLevel = Common.Error.Validation("subjectId", "Choose a subject and class level.");
}

internal static class CbtQueries
{
    public static IQueryable<QuestionDto> QuestionDtos(this IAppDbContext db, IQueryable<Question> questions) =>
        questions
            .Join(db.Subjects, q => q.SubjectId, s => s.Id, (q, s) => new { q, s })
            .Join(db.ClassLevels, x => x.q.ClassLevelId, l => l.Id, (x, l) => new { x.q, x.s, l })
            .Select(x => new QuestionDto(x.q.Id, x.q.SubjectId, x.s.Name, x.q.ClassLevelId, x.l.Name, x.q.Kind, x.q.Text, x.q.ImagePath != null,
                x.q.Options.OrderBy(o => o.Order).Select(o => new OptionDto(o.Id, o.Text, o.IsCorrect)).ToList(),
                x.q.TrueFalseAnswer, x.q.AcceptedAnswers, x.q.MarkingGuide, x.q.Topic, x.q.Difficulty, x.q.Source,
                x.q.Kind != QuestionKind.Theory && (x.q.Kind != QuestionKind.ShortAnswer || x.q.AcceptedAnswers.Count > 0),
                db.Users.Where(u => u.Id == x.q.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(), x.q.CreatedAt, x.q.UpdatedAt));

    /// <summary>Copies the input onto the question (new options get new ids; kept ids keep their identity).</summary>
    public static void Apply(this Question q, QuestionInput input)
    {
        (q.SubjectId, q.ClassLevelId, q.Kind, q.Text, q.TrueFalseAnswer, q.MarkingGuide, q.Topic, q.Difficulty) =
            (input.SubjectId, input.ClassLevelId, input.Kind, input.Text ?? "", input.TrueFalseAnswer, input.MarkingGuide, input.Topic, input.Difficulty);
        q.AcceptedAnswers = [.. input.AcceptedAnswers ?? []];
        var existing = q.Options.ToDictionary(o => o.Id);
        q.Options = [.. (input.Options ?? []).Select(o => o.Id is { } id && existing.TryGetValue(id, out var kept)
            ? Keep(kept, o)
            : new QuestionOption { Text = o.Text ?? "", IsCorrect = o.IsCorrect })];

        static QuestionOption Keep(QuestionOption kept, OptionInput o)
        {
            (kept.Text, kept.IsCorrect) = (o.Text ?? "", o.IsCorrect);
            return kept;
        }
    }
}
