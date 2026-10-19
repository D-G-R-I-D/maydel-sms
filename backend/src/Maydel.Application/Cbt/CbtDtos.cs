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

/// <summary>The test's header as the teacher sets it. Times are instants (send them with an offset).</summary>
public sealed record TestInput(
    string Title, string? Instructions, Guid SubjectId, Guid ClassLevelId, Guid TermId, Guid? ComponentId, IReadOnlyList<Guid> ClassRoomIds,
    int DurationMinutes, DateTimeOffset OpensAt, DateTimeOffset ClosesAt, int MaxAttempts = 1, bool ShuffleQuestions = true, bool ShuffleOptions = true);

/// <summary>A section: one kind of question, marks per question, and fixed questions or a random draw from the bank.</summary>
public sealed record SectionInput(
    string Title, string? Instructions, QuestionKind Kind, decimal MarksPerQuestion, SectionMode Mode, IReadOnlyList<Guid>? QuestionIds,
    int? RandomCount, string? Topic, Difficulty? Difficulty);

public sealed record SectionsInput(IReadOnlyList<SectionInput> Sections);

public sealed record QuestionBrief(Guid Id, QuestionKind Kind, string Text, string? Topic, Difficulty Difficulty, bool HasImage, bool IsArchived);

/// <summary><c>AvailableInBank</c>: random sections only, how many live questions match the draw.</summary>
public sealed record SectionDto(
    Guid Id, int Order, string Title, string? Instructions, QuestionKind Kind, decimal MarksPerQuestion, SectionMode Mode, int QuestionCount,
    decimal Marks, int? RandomCount, string? Topic, Difficulty? Difficulty, IReadOnlyList<QuestionBrief> Questions, int? AvailableInBank);

/// <summary>How the raw total maps to the gradebook column, e.g. 100 marks → Exams / 40 (×0.4).</summary>
public sealed record ScalingPreview(Guid ComponentId, string Component, decimal ComponentMax, decimal TotalMarks, decimal Factor);

public sealed record ClassRoomBrief(Guid Id, string Name);

public sealed record TestSummary(
    Guid Id, string Title, Guid SubjectId, string Subject, Guid ClassLevelId, string ClassLevel, Guid TermId, string? Component, TestStatus Status,
    DateTimeOffset OpensAt, DateTimeOffset ClosesAt, int DurationMinutes, int QuestionCount, decimal TotalMarks, IReadOnlyList<string> ClassRooms);

/// <summary>A test with its sections, totals, scaling and what still blocks publishing (<c>Problems</c> empty = ready).</summary>
public sealed record TestDetail(
    Guid Id, string Title, string? Instructions, Guid SubjectId, string Subject, Guid ClassLevelId, string ClassLevel, Guid TermId, string Term,
    Guid? ComponentId, IReadOnlyList<ClassRoomBrief> ClassRooms, int DurationMinutes, DateTimeOffset OpensAt, DateTimeOffset ClosesAt,
    int MaxAttempts, bool ShuffleQuestions, bool ShuffleOptions, TestStatus Status, DateTimeOffset? PublishedAt, IReadOnlyList<SectionDto> Sections,
    int QuestionCount, decimal TotalMarks, ScalingPreview? Scaling, IReadOnlyList<string> Problems, bool CanEdit,
    DateTimeOffset? ResultsReleasedAt, DateTimeOffset? GradebookPushedAt);

public enum AttemptRowState
{
    NotStarted,
    InProgress,
    Submitted,
}

/// <summary>One student on the test's results page. <c>Score</c>: total once every answer is marked (best attempt).</summary>
public sealed record AttemptRow(
    Guid StudentId, string Name, string AdmissionNumber, string? ClassRoom, Guid? AttemptId, int Attempts, AttemptRowState State,
    DateTimeOffset? StartedAt, DateTimeOffset? SubmittedAt, bool AutoSubmitted, int Blurs, int TakeOvers, decimal? AutoScore, decimal? Score, int NeedsMarking);

public sealed record AttemptRows(Guid TestId, decimal TotalMarks, DateTimeOffset? ResultsReleasedAt, int Sitting, int NeedsMarking, IReadOnlyList<AttemptRow> Students);

public sealed record ReviewOption(Guid Id, string Text, bool IsCorrect, bool Chosen);

/// <summary>An answer with everything the marker needs: the key or guide, the student's answer, and the marks so far.</summary>
public sealed record ReviewItem(
    Guid Id, int Order, string Section, QuestionKind Kind, Guid QuestionId, string Text, bool HasImage, decimal Marks,
    IReadOnlyList<ReviewOption> Options, bool? CorrectTrueFalse, IReadOnlyList<string> AcceptedAnswers, string? MarkingGuide,
    bool? AnswerTrueFalse, string? AnswerText, bool Answered, decimal? AutoScore, decimal? TeacherScore, decimal? Score, string? Comment);

public sealed record IntegrityEventDto(IntegrityEventKind Kind, DateTimeOffset At);

public sealed record AttemptReview(
    Guid Id, Guid TestId, Guid StudentId, string StudentName, string AdmissionNumber, int Number, AttemptStatus Status, DateTimeOffset StartedAt,
    DateTimeOffset DeadlineAt, DateTimeOffset? SubmittedAt, bool AutoSubmitted, IReadOnlyList<IntegrityEventDto> Events, IReadOnlyList<ReviewItem> Items,
    decimal TotalMarks, decimal? Score, int NeedsMarking, bool CanMark);

public sealed record MarkInput(decimal Score, string? Comment);

/// <summary>Every student's answer to one question, for marking theory question by question.</summary>
public sealed record QuestionResponses(Guid QuestionId, QuestionKind Kind, string Text, string? MarkingGuide, IReadOnlyList<string> AcceptedAnswers,
    IReadOnlyList<QuestionResponse> Responses);

public sealed record QuestionResponse(Guid AttemptId, Guid ItemId, string StudentName, decimal Marks, string? AnswerText, bool Answered,
    decimal? AutoScore, decimal? Score, string? Comment);

public enum PushState
{
    Ready,
    /// <summary>The student's report for the term is published: never overwritten (E16.5).</summary>
    ReportPublished,
    /// <summary>The student isn't (any more) an active member of one of the test's classes.</summary>
    NotInClass,
    /// <summary>The caller isn't the subject or form teacher of the student's class.</summary>
    NotYourClass,
}

public sealed record GradebookRow(Guid StudentId, string Name, string? ClassRoom, decimal Raw, decimal Scaled, decimal? Current, PushState State);

public sealed record GradebookPreview(Guid TestId, string Component, decimal ComponentMax, decimal TotalMarks, IReadOnlyList<GradebookRow> Rows);

public sealed record PushInput(IReadOnlyList<Guid>? StudentIds);

public sealed record PushResult(int Pushed, IReadOnlyList<GradebookRow> Skipped);

public sealed record ScoreBand(int FromPercent, int ToPercent, int Students);

public sealed record ClassResult(string ClassRoom, int Students, decimal Mean);

public sealed record OptionPicks(Guid OptionId, string Text, bool IsCorrect, int Picks);

/// <summary>How one question went: <c>Facility</c> = average share of the marks earned (0–1); low means hard.</summary>
public sealed record ItemAnalysis(Guid QuestionId, QuestionKind Kind, string Text, string Section, decimal Marks, int Responses, int Answered,
    decimal? Facility, IReadOnlyList<OptionPicks> Options);

/// <summary>Score spread (best attempt per student, fully marked only) and per-question analysis (E16.7).</summary>
public sealed record TestAnalytics(
    Guid TestId, decimal TotalMarks, int Students, decimal? Mean, decimal? Median, decimal? Highest, decimal? Lowest, IReadOnlyList<ScoreBand> Distribution,
    IReadOnlyList<ClassResult> ByClass, IReadOnlyList<ItemAnalysis> Items);

/// <summary>A released test result for the parent portal.</summary>
public sealed record ChildTestResult(Guid TestId, string Title, string Subject, DateTimeOffset? SubmittedAt, decimal Score, decimal TotalMarks, decimal Percent);

public enum StudentTestState
{
    Upcoming,
    Open,
    InProgress,
    /// <summary>Handed in and no attempts left (or the window closed after an attempt).</summary>
    Done,
    /// <summary>The window closed without an attempt.</summary>
    Missed,
}

/// <summary>A test as a student sees it in their list (no questions, no keys).</summary>
public sealed record StudentTestDto(
    Guid Id, string Title, string Subject, string? Instructions, int DurationMinutes, DateTimeOffset OpensAt, DateTimeOffset ClosesAt,
    int QuestionCount, decimal TotalMarks, int MaxAttempts, int AttemptsUsed, StudentTestState State, Guid? ActiveAttemptId,
    bool ResultsReleased, decimal? Score, Guid? ResultAttemptId);

public sealed record PaperOption(Guid Id, string Text);

/// <summary>A question on the student's paper: no answer key. <c>ImageUrl</c> is relative to the API.</summary>
public sealed record PaperItem(
    Guid Id, int Order, int Section, QuestionKind Kind, string Text, string? ImageUrl, decimal Marks, IReadOnlyList<PaperOption> Options,
    IReadOnlyList<Guid> AnswerOptionIds, bool? AnswerTrueFalse, string? AnswerText, bool Answered, decimal? Score, string? Comment);

public sealed record PaperSection(int Index, string Title, string? Instructions, QuestionKind Kind, decimal MarksPerQuestion);

/// <summary>
/// The student's paper. Use <c>ServerNow</c> and <c>DeadlineAt</c> (not the device clock) for the countdown; send <c>SessionToken</c>
/// with every save. Status Submitted: read-only.
/// </summary>
public sealed record AttemptPaper(
    Guid Id, Guid TestId, string Title, string? Instructions, AttemptStatus Status, DateTimeOffset StartedAt, DateTimeOffset DeadlineAt,
    DateTimeOffset ServerNow, int RemainingSeconds, string? SessionToken, IReadOnlyList<PaperSection> Sections, IReadOnlyList<PaperItem> Items,
    int Answered, DateTimeOffset? SubmittedAt, bool AutoSubmitted, bool Released, decimal? Score, decimal TotalMarks);

public sealed record AnswerSaved(Guid ItemId, DateTimeOffset SavedAt, int RemainingSeconds);

public sealed record AnswerInput(string SessionToken, IReadOnlyList<Guid>? OptionIds, bool? TrueFalse, string? Text);

public sealed record TokenInput(string SessionToken);

public sealed record EventInput(string SessionToken, IntegrityEventKind Kind);

public static class CbtErrors
{
    public static readonly Common.Error NoComponent = Common.Error.Rule("test.no_component", "This is a practice test: it isn't linked to a gradebook column.");
    public static readonly Common.Error NotReleased = Common.Error.Rule("test.not_released", "Release the results before sending them to the grade sheet.");
    public static readonly Common.Error AttemptNotFound = Common.Error.NotFound("attempt.not_found", "That test attempt doesn't exist.");
    public static readonly Common.Error NotAStudent = Common.Error.Forbidden("attempt.not_student", "Only students can sit online tests.");
    public static readonly Common.Error TestNotFound = Common.Error.NotFound("test.not_found", "That test doesn't exist.");
    public static readonly Common.Error TermNotFound = Common.Error.Validation("termId", "Choose a term.");
    public static readonly Common.Error ComponentNotFound = Common.Error.Validation("componentId", "Choose an active assessment component (or none for practice).");
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
