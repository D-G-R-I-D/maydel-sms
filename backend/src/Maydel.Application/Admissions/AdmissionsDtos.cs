using Maydel.Application.Abstractions;
using Maydel.Domain.Admissions;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions;

public sealed record ClassLevelOption(Guid Id, string Name);

/// <summary>What the public "Buy admission form" page shows for one session that's open (or not).</summary>
public sealed record AdmissionsInfo(Guid SessionId, string Session, bool IsOpen, decimal FormFee, int ExamWindowDays, string? Instructions,
    IReadOnlyList<ClassLevelOption> ClassLevels);

public sealed record AdmissionSettingsDto(Guid SessionId, string Session, bool IsOpen, decimal FormFee, int ExamWindowDays, string? Instructions);

/// <summary>An application as the family and staff see it. <c>ExamWindowEndsOn</c> is set once the form is paid for.</summary>
public sealed record ApplicationDto(
    Guid Id, string ReferenceNumber, Guid SessionId, string Session, Guid ClassLevelId, string ClassLevel,
    string ParentName, string Phone, string Email, string ChildFirstName, string ChildLastName, DateOnly? ChildDateOfBirth,
    ApplicationStatus Status, decimal FormFee, FormPaymentMethod? FormPaymentMethod, DateTimeOffset? FormPaidAt, DateOnly? ExamWindowEndsOn,
    DateTimeOffset CreatedAt);

/// <summary><c>Code</c> is shown once to the bursar to hand to the family; <c>Used</c> once redeemed.</summary>
public sealed record FormCodeDto(Guid Id, string Code, Guid SessionId, decimal AmountPaid, string? IssuedTo, bool Used, string? UsedByReference, DateTimeOffset IssuedAt);

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
    public static readonly Common.Error NotAwaitingPayment = Common.Error.Rule("admission.already_paid", "The admission form for this application is already paid for.");
}

internal static class AdmissionQueries
{
    public static IQueryable<ApplicationDto> Dtos(this IAppDbContext db, IQueryable<AdmissionApplication> applications) =>
        applications.Join(db.Sessions, a => a.SessionId, s => s.Id, (a, s) => new { a, s })
            .Join(db.ClassLevels, x => x.a.ClassLevelId, l => l.Id, (x, l) => new { x.a, x.s, l })
            .OrderByDescending(x => x.a.CreatedAt)
            .Select(x => new ApplicationDto(x.a.Id, x.a.ReferenceNumber, x.a.SessionId, x.s.Name, x.a.ClassLevelId, x.l.Name,
                x.a.ParentName, x.a.Phone, x.a.Email, x.a.ChildFirstName, x.a.ChildLastName, x.a.ChildDateOfBirth,
                x.a.Status, x.a.FormFee, x.a.FormPaymentMethod, x.a.FormPaidAt, x.a.ExamWindowEndsOn, x.a.CreatedAt));

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
