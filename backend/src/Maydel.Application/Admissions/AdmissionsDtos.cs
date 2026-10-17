using Maydel.Application.Abstractions;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions;

public sealed record ClassLevelOption(Guid Id, string Name);

/// <summary>What the public "Buy admission form" page shows for one session that's open (or not).</summary>
public sealed record AdmissionsInfo(Guid SessionId, string Session, bool IsOpen, decimal FormFee, int ExamWindowDays, string? Instructions,
    IReadOnlyList<ClassLevelOption> ClassLevels);

public sealed record AdmissionSettingsDto(Guid SessionId, string Session, bool IsOpen, decimal FormFee, int ExamWindowDays, string? Instructions, decimal? CutOffMark);

/// <summary>An application as the family and staff see it. <c>ExamWindowEndsOn</c> is set once the form is paid for.</summary>
public sealed record ApplicationDto(
    Guid Id, string ReferenceNumber, Guid SessionId, string Session, Guid ClassLevelId, string ClassLevel,
    string ParentName, string Phone, string Email, string ChildFirstName, string ChildLastName, DateOnly? ChildDateOfBirth,
    ApplicationStatus Status, decimal FormFee, FormPaymentMethod? FormPaymentMethod, DateTimeOffset? FormPaidAt, DateOnly? ExamWindowEndsOn,
    DateTimeOffset CreatedAt, ExamBooking? Exam, string? AdmissionNumber, Guid? StudentId);

/// <summary>The family's booked exam sitting (null until booked).</summary>
public sealed record ExamBooking(Guid SlotId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Venue, ExamMode Mode);

/// <summary>An exam sitting as staff manage it. Times are UTC instants; show them in Abuja time.</summary>
public sealed record ExamSlotDto(Guid Id, Guid SessionId, DateTimeOffset StartsAt, int DurationMinutes, string Venue, ExamMode Mode, int Capacity, int Booked, string? Notes);

/// <summary>A sitting a family can book: in their window, in the future, with seats left (their current one is flagged).</summary>
public sealed record ExamSlotOption(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Venue, ExamMode Mode, int SeatsLeft, bool Booked);

/// <summary>One applicant in a sitting (attendance sheet / score entry) or on the results lists.</summary>
public sealed record ExamCandidate(Guid ApplicationId, string ReferenceNumber, string ChildName, string ClassLevel, ApplicationStatus Status,
    decimal? Score, DateTimeOffset? StageOneNotifiedAt, OfferDecision? Decision);

/// <summary>
/// The entrance exam results for a session (E11.7). <c>Passed</c>/<c>Failed</c> are worked out from the current cut-off on every read,
/// so changing it changes the lists before anything is sent. Both are empty until a cut-off is set.
/// </summary>
public sealed record ExamResults(Guid SessionId, decimal? CutOffMark, IReadOnlyList<ExamCandidate> Taken, IReadOnlyList<ExamCandidate> Passed,
    IReadOnlyList<ExamCandidate> Failed, int AwaitingScore, int Missed);

/// <summary><c>Code</c> is shown once to the bursar to hand to the family; <c>Used</c> once redeemed.</summary>
public sealed record FormCodeDto(Guid Id, string Code, Guid SessionId, decimal AmountPaid, string? IssuedTo, bool Used, string? UsedByReference, DateTimeOffset IssuedAt);

public sealed record ApplicationDocumentDto(Guid Id, Domain.Admissions.DocumentKind Kind, string ContentType, DateTimeOffset UploadedAt);

/// <summary>An application with its form and documents (family's own, or staff).</summary>
public sealed record ApplicationDetail(ApplicationDto Application, ApplicationForm Form, IReadOnlyList<ApplicationDocumentDto> Documents,
    DateTimeOffset? SubmittedAt, bool CanEdit, IReadOnlyList<string> MissingToSubmit, decimal? ExamScore);

/// <summary>The draft form: child's names and date of birth plus every form field.</summary>
public sealed record SaveFormRequest(string ChildFirstName, string ChildLastName, DateOnly? ChildDateOfBirth, ApplicationForm Form);

public sealed record ApplyChild(string ChildFirstName, string ChildLastName, DateOnly? ChildDateOfBirth, Guid ClassLevelId, Guid SessionId);

public static class AdmissionErrors
{
    public static readonly Common.Error Closed = Common.Error.Rule("admission.closed", "Admissions aren't open for that session.");
    public static readonly Common.Error SessionNotFound = Common.Error.NotFound("session.not_found", "That session doesn't exist.");
    public static readonly Common.Error ClassLevelNotFound = Common.Error.Validation("classLevelId", "Choose a class to apply for.");
    public static readonly Common.Error ApplicationNotFound = Common.Error.NotFound("admission.not_found", "We couldn't find that application on your account.");
    public static readonly Common.Error EmailInUse = Common.Error.Conflict("admission.email_in_use",
        "An account with this email already exists. Sign in with it and apply from your account.");
    public static readonly Common.Error BadCode = Common.Error.Validation("code", "That form code isn't valid for this session, or it has already been used.");
    public static readonly Common.Error DocumentNotFound = Common.Error.NotFound("admission.document_not_found", "That document doesn't exist.");
    public static readonly Common.Error TooManyDocuments = Common.Error.Rule("admission.too_many_documents",
        $"Upload at most {ApplicationDocument.MaxPerApplication} documents. Remove one to add another.");
    public static readonly Common.Error SlotNotFound = Common.Error.NotFound("exam_slot.not_found", "That exam time doesn't exist.");
    public static readonly Common.Error SlotTaken = Common.Error.Conflict("exam_slot.busy", "Someone booked that exam time at the same moment. Please try again.");
    public static readonly Common.Error SlotHasBookings = Common.Error.Rule("exam_slot.has_bookings",
        "Applicants are booked for this exam time, so it can't be moved or deleted. Add another time instead.");
    public static readonly Common.Error NoCutOff = Common.Error.Rule("admission.no_cut_off", "Set the cut-off mark first.");
    public static readonly Common.Error NoLetter = Common.Error.NotFound("admission.no_letter", "There's no admission letter for this application.");
    public static readonly Common.Error NotAwaitingPayment = Common.Error.Rule("admission.already_paid", "The admission form for this application is already paid for.");
}

internal static class AdmissionQueries
{
    /// <summary>The family's own application (tracked), or null.</summary>
    public static Task<AdmissionApplication?> MineAsync(this IAppDbContext db, Guid id, Guid? userId, CancellationToken ct) =>
        db.Applications.FirstOrDefaultAsync(a => a.Id == id && a.ApplicantUserId == userId, ct);

    public static async Task<ApplicationDetail?> DetailAsync(this IAppDbContext db, Guid id, CancellationToken ct, bool asStaff = false)
    {
        var app = await db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null) return null;
        var dto = await db.Dtos(db.Applications.Where(a => a.Id == id)).FirstAsync(ct);
        var missing = new List<string>();
        if (app.ChildDateOfBirth is null) missing.Add("childDateOfBirth");
        if (app.Form.Missing() is { Field: { } field }) missing.Add(field);
        if (app.Documents.All(d => d.Kind != DocumentKind.PassportPhoto)) missing.Add("documents");
        return new ApplicationDetail(dto, app.Form, [.. app.Documents.OrderBy(d => d.UploadedAt).Select(d => new ApplicationDocumentDto(d.Id, d.Kind, d.ContentType, d.UploadedAt))],
            app.SubmittedAt, app.Status == ApplicationStatus.FormPaid, missing, asStaff ? app.ExamScore : null);
    }

    public static IQueryable<ApplicationDto> Dtos(this IAppDbContext db, IQueryable<AdmissionApplication> applications) =>
        applications.Join(db.Sessions, a => a.SessionId, s => s.Id, (a, s) => new { a, s })
            .Join(db.ClassLevels, x => x.a.ClassLevelId, l => l.Id, (x, l) => new { x.a, x.s, l })
            .OrderByDescending(x => x.a.CreatedAt)
            .Select(x => new ApplicationDto(x.a.Id, x.a.ReferenceNumber, x.a.SessionId, x.s.Name, x.a.ClassLevelId, x.l.Name,
                x.a.ParentName, x.a.Phone, x.a.Email, x.a.ChildFirstName, x.a.ChildLastName, x.a.ChildDateOfBirth,
                x.a.Status, x.a.FormFee, x.a.FormPaymentMethod, x.a.FormPaidAt, x.a.ExamWindowEndsOn, x.a.CreatedAt,
                db.ExamSlots.Where(s => s.Id == x.a.ExamSlotId)
                    .Select(s => new ExamBooking(s.Id, s.StartsAt, s.StartsAt.AddMinutes(s.DurationMinutes), s.Venue, s.Mode)).FirstOrDefault(),
                x.a.AdmissionNumber, x.a.StudentId));

    /// <summary>Applicants in a sitting or session, for staff lists.</summary>
    public static IQueryable<ExamCandidate> Candidates(this IAppDbContext db, IQueryable<AdmissionApplication> applications) =>
        applications.Join(db.ClassLevels, a => a.ClassLevelId, l => l.Id, (a, l) => new { a, l })
            .OrderBy(x => x.a.ChildLastName).ThenBy(x => x.a.ChildFirstName)
            .Select(x => new ExamCandidate(x.a.Id, x.a.ReferenceNumber, x.a.ChildFirstName + " " + x.a.ChildLastName, x.l.Name, x.a.Status,
                x.a.ExamScore, x.a.StageOneNotifiedAt, x.a.Decision));

    /// <summary>"APP-2026-0007": numbered per session by its start year.</summary>
    public static async Task<string> NextReferenceAsync(this IAppDbContext db, Guid sessionId, CancellationToken ct)
    {
        var year = await db.Sessions.Where(s => s.Id == sessionId).Select(s => s.StartsOn.Year).FirstAsync(ct);
        var prefix = $"APP-{year}-";
        var used = await db.Applications.Where(a => a.ReferenceNumber.StartsWith(prefix)).Select(a => a.ReferenceNumber).ToListAsync(ct);
        var next = used.Count == 0 ? 1 : used.Max(r => int.Parse(r[prefix.Length..])) + 1;
        return $"{prefix}{next:0000}";
    }

    public static async Task<Common.Error?> CheckOpenAsync(this IAppDbContext db, Guid sessionId, Guid classLevelId, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == sessionId, ct)) return AdmissionErrors.SessionNotFound;
        if (!await db.AdmissionSettings.AnyAsync(s => s.SessionId == sessionId && s.IsOpen, ct)) return AdmissionErrors.Closed;
        if (!await db.ClassLevels.AnyAsync(l => l.Id == classLevelId, ct)) return AdmissionErrors.ClassLevelNotFound;
        return null;
    }
}
