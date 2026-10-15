using Maydel.Application.Assessment;
using Maydel.Domain.Academics;
using Maydel.Domain.Assessment;
using Maydel.Domain.People;

namespace Maydel.Application.Reports;

/// <summary>One line of the class report list. Draft rows show live figures; published rows show the frozen ones.</summary>
public sealed record ClassReportRow(
    Guid StudentId, string AdmissionNumber, string Name, int SubjectsScored, decimal? Average, int? Position,
    ReportStatus Status, int? DaysPresent, bool HasFormTeacherComment, bool HasPrincipalComment, int TraitsRated);

public sealed record ClassReportSummary(
    Guid ClassRoomId, string ClassRoom, Guid TermId, string Term, string? FormTeacher, int SubjectsOffered,
    decimal ClassAverage, int? DaysOpened, int PublishedCount, int DraftCount, IReadOnlyList<ClassReportRow> Rows);

public sealed record ReportSubjectLine(
    Guid SubjectId, string Subject, IReadOnlyList<ComponentScoreValue> Scores, decimal Total, string? Grade, string? Remark,
    string? Comment, decimal ClassHighest, decimal ClassLowest, decimal ClassAverage,
    IReadOnlyList<PreviousSubjectResult> PreviousTerms, decimal? ChangeFromLastTerm, Trend? Trend);

/// <summary>This subject's total in an earlier (published) term of the same session.</summary>
public sealed record PreviousSubjectResult(TermNumber Number, string Term, decimal Total, string? Grade);

/// <summary>The student's published result in an earlier term of the same session.</summary>
public sealed record PreviousTermResult(Guid TermId, TermNumber Number, string Term, decimal Average, int? Position, int ClassSize);

/// <summary>The full report card (E5.4): preview for drafts, frozen figures once published.</summary>
public sealed record ReportCard(
    Guid StudentId, string StudentName, string AdmissionNumber, Sex? Sex, DateOnly? DateOfBirth, string? House, string? PhotoVersion,
    Guid ClassRoomId, string ClassRoom, Guid TermId, string Term, string Session, DateOnly TermEndsOn, DateOnly? NextTermBegins,
    ReportStatus Status, DateTimeOffset? PublishedAt,
    IReadOnlyList<GradeSheetComponent> Components, IReadOnlyList<ReportSubjectLine> Subjects,
    decimal? Average, decimal ClassAverage, int? Position, int ClassSize, decimal TotalScore,
    IReadOnlyList<PreviousTermResult> PreviousTerms, decimal? AverageChange, Trend? Trend, decimal? CumulativeAverage,
    int? DaysOpened, int? DaysPresent, int? DaysAbsent,
    string? FormTeacher, string? FormTeacherComment, string? PrincipalComment, IReadOnlyList<GradeBandDto> GradingKey,
    IReadOnlyList<TraitRatingLine> Behaviour, IReadOnlyList<ClubLine> Clubs, IReadOnlyList<RatingKeyEntry> BehaviourKey);

/// <summary>A behaviour trait on the report (E2.5): every active trait, plus inactive ones this report rated. <c>Rating</c> null = not rated.</summary>
public sealed record TraitRatingLine(Guid TraitId, string Trait, string? Rating);

/// <summary>A club or activity on the report, with an optional free-text rating such as "Excellent".</summary>
public sealed record ClubLine(string Name, string? Rating);

/// <summary><c>Rating</c>: a letter from the key (A–E, any case); null or blank leaves the trait unrated.</summary>
public sealed record TraitRatingInput(Guid TraitId, string? Rating);

/// <summary>Replaces the report's behaviour ratings and clubs. Traits left out are unrated.</summary>
public sealed record BehaviourRequest(IReadOnlyList<TraitRatingInput>? Ratings, IReadOnlyList<ClubLine>? Clubs);

public sealed record CommentRequest(string? Comment);

public sealed record AttendanceEntry(Guid StudentId, int DaysPresent);

/// <summary>
/// <c>Published</c>: reports newly published by this call. <c>AlreadyPublished</c>: reports that were published before
/// (their positions and class average are refreshed, nothing else changes and their parents aren't emailed again).
/// <c>Skipped</c>: active students in the class who weren't published, with the reason (e.g. no scores yet).
/// <c>EmailsQueued</c>: guardian emails queued with the report attached (0 when <c>notifyParents</c> was false).
/// </summary>
public sealed record PublishResult(int Published, decimal ClassAverage, IReadOnlyList<SkippedStudent> Skipped, int EmailsQueued, int AlreadyPublished);

public sealed record SkippedStudent(Guid StudentId, string Name, string Reason);

public sealed record UnpublishResult(int Unpublished);

public static class ReportErrors
{
    public static readonly Common.Error NotAllowed = Common.Error.Forbidden("report.not_allowed", "Only administrators and this class's teachers can see its reports.");
    public static readonly Common.Error NotFormTeacher = Common.Error.Forbidden("report.not_form_teacher", "Only the class's form teacher or an administrator can do this.");
    public static readonly Common.Error ClassNotFound = Common.Error.NotFound("classroom.not_found", "That class doesn't exist.");
    public static readonly Common.Error TermNotFound = Common.Error.NotFound("term.not_found", "That term doesn't exist.");
    public static readonly Common.Error StudentNotFound = Common.Error.NotFound("student.not_found", "That student doesn't exist.");
    public static readonly Common.Error NoClass = Common.Error.Rule("report.no_class", "This student isn't in a class, so there's no report to show.");
    public static readonly Common.Error AlreadyPublished = Common.Error.Rule("report.already_published",
        "Every report in this class is already published. To change one, unpublish it, make the change, then publish again.");
    public static readonly Common.Error NothingToPublish = Common.Error.Rule("report.nothing_to_publish", "No student in this class has scores for this term yet.");
    public static Common.Error AttendanceMissing(IEnumerable<string> names) => Common.Error.Rule("report.attendance_missing",
        $"Enter attendance before publishing. Missing for: {string.Join(", ", names)}.");
}

/// <summary>A generated PDF and a suggested download file name.</summary>
public sealed record PdfFile(byte[] Content, string FileName);
