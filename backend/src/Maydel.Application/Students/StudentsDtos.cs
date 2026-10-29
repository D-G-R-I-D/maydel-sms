using Maydel.Application.Abstractions;
using Maydel.Application.Identity;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students;

public sealed record StudentListItem(
    Guid Id, string AdmissionNumber, string FullName, string FirstName, string LastName, Sex? Sex,
    Guid? ClassRoomId, string? ClassRoom, StudentStatus Status, bool HasLogin, string? PhotoVersion);

public sealed record GuardianLink(
    Guid GuardianId, string FullName, string Phone, string? Email, GuardianRelationship Relationship,
    bool IsPrimaryContact, bool CanPickUp, bool HasLogin);

public sealed record StudentDetail(
    Guid Id, string AdmissionNumber, string FirstName, string? MiddleName, string LastName, string FullName,
    DateOnly? DateOfBirth, Sex? Sex, string? StateOfOrigin, string? Lga, string? HomeAddress, string? House, string? MedicalNotes,
    DateOnly AdmittedOn, StudentStatus Status, Guid? ClassRoomId, string? ClassRoom, bool HasLogin, string? PhotoVersion,
    IReadOnlyList<GuardianLink> Guardians);

/// <summary>Create/update body. <c>AdmittedOn</c> also fixes the admission-number year on create.</summary>
public sealed record StudentRequest(
    string FirstName, string? MiddleName, string LastName, DateOnly? DateOfBirth, Sex? Sex,
    string? StateOfOrigin, string? Lga, string? HomeAddress, string? House, string? MedicalNotes,
    Guid? ClassRoomId, DateOnly AdmittedOn);

public sealed record StudentStatusRequest(StudentStatus Status);

public sealed record GuardianRequest(
    string FullName, string Phone, string? Email, string? Occupation, string? Address,
    GuardianRelationship Relationship, bool IsPrimaryContact, bool CanPickUp = true);

public sealed record GuardianLinkRequest(GuardianRelationship Relationship, bool IsPrimaryContact, bool CanPickUp);

/// <summary>
/// After adding a guardian. <c>ReusedExistingGuardian</c> = a guardian with that phone already existed (a sibling's parent)
/// and was linked as-is: their stored name/email were kept, not overwritten.
/// </summary>
public sealed record GuardianAdded(StudentDetail Student, Guid GuardianId, bool ReusedExistingGuardian);

public sealed record GuardianChild(Guid StudentId, string AdmissionNumber, string FullName, string? ClassRoom, StudentStatus Status, GuardianRelationship Relationship);

public sealed record GuardianListItem(Guid Id, string FullName, string Phone, string? Email, int ChildCount, bool HasLogin);

public sealed record GuardianDetail(
    Guid Id, string FullName, string Phone, string? Email, string? Occupation, string? Address, bool HasLogin, IReadOnlyList<GuardianChild> Children);

public sealed record GuardianUpdateRequest(string FullName, string Phone, string? Email, string? Occupation, string? Address);

/// <summary><c>Credentials</c> null = the email already had an account (e.g. staff), which was linked; they keep their password.</summary>
public sealed record GuardianLoginIssued(IssuedCredentials? Credentials, bool LinkedExistingAccount);

public static class StudentErrors
{
    public static readonly Common.Error NotFound = Common.Error.NotFound("student.not_found", "That student doesn't exist.");
    public static readonly Common.Error GuardianNotFound = Common.Error.NotFound("guardian.not_found", "That guardian doesn't exist.");
    public static readonly Common.Error NotLinked = Common.Error.NotFound("guardian.not_linked", "That guardian isn't linked to this student.");
    public static readonly Common.Error AlreadyLinked = Common.Error.Conflict("guardian.already_linked", "That guardian is already linked to this student.");
    public static readonly Common.Error PhoneInUse = Common.Error.Conflict("guardian.phone_in_use", "Another guardian already uses that phone number.");
    public static readonly Common.Error LoginInUse = Common.Error.Conflict("guardian.login_in_use", "That email or phone belongs to an account that can't be used for a parent login.");
    public static readonly Common.Error InactiveStudent = Common.Error.Rule("student.inactive", "Only active students can have a login. Change the student's status first.");
    public static readonly Common.Error NoClass = Common.Error.Validation("classRoomId", "Choose a class.");
    public static Common.Error ClassFull(string name, int capacity) => Common.Error.Rule("classroom.full", $"{name} is full ({capacity} students). Raise its capacity or choose another class.");
    public static readonly Common.Error InvalidPhone = Common.Error.Validation("phone", "Enter a phone number, e.g. 0803 000 0000 or +234 803 000 0000.");
}

public static class StudentQueries
{
    public static async Task<StudentDetail?> GetStudentDetailAsync(this IAppDbContext db, Guid id, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id, s.AdmissionNumber, s.FirstName, s.MiddleName, s.LastName, s.DateOfBirth, s.Sex, s.StateOfOrigin, s.Lga,
                s.HomeAddress, s.House, s.MedicalNotes, s.AdmittedOn, s.Status, s.ClassRoomId,
                ClassRoom = s.ClassRoom != null ? s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm : null,
                HasLogin = s.UserId != null,
                s.PhotoPath,
                Guardians = s.Guardians.OrderByDescending(g => g.IsPrimaryContact).ThenBy(g => g.Guardian.FullName)
                    .Select(g => new GuardianLink(g.GuardianId, g.Guardian.FullName, g.Guardian.Phone, g.Guardian.Email, g.Relationship,
                        g.IsPrimaryContact, g.CanPickUp, g.Guardian.UserId != null))
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (student is null) return null;

        var fullName = string.Join(' ', new[] { student.FirstName, student.MiddleName, student.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return new StudentDetail(
            student.Id, student.AdmissionNumber, student.FirstName, student.MiddleName, student.LastName, fullName, student.DateOfBirth,
            student.Sex, student.StateOfOrigin, student.Lga, student.HomeAddress, student.House, student.MedicalNotes, student.AdmittedOn,
            student.Status, student.ClassRoomId, student.ClassRoom, student.HasLogin, Photos.PhotoStore.VersionOf(student.PhotoPath), student.Guardians);
    }

    public static async Task<GuardianDetail?> GetGuardianDetailAsync(this IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Guardians.AsNoTracking().Where(g => g.Id == id)
            .Select(g => new GuardianDetail(
                g.Id, g.FullName, g.Phone, g.Email, g.Occupation, g.Address, g.UserId != null,
                g.Students.OrderBy(sg => sg.Student.FirstName)
                    .Select(sg => new GuardianChild(
                        sg.StudentId, sg.Student.AdmissionNumber, sg.Student.FirstName + " " + sg.Student.LastName,
                        sg.Student.ClassRoom != null ? sg.Student.ClassRoom.ClassLevel.Name + " " + sg.Student.ClassRoom.Arm : null,
                        sg.Student.Status, sg.Relationship))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

    /// <summary>Next MSS/{year}/#### for the admission year.</summary>
    public static async Task<string> NextAdmissionNumberAsync(this IAppDbContext db, int year, CancellationToken ct)
    {
        var prefix = Student.AdmissionPrefix(year);
        // Numbers reserved by published admission offers count too, so a letter's number is never given to someone else.
        var numbers = await db.Students.Where(s => s.AdmissionNumber.StartsWith(prefix)).Select(s => s.AdmissionNumber)
            .Concat(db.Applications.Where(a => a.AdmissionNumber != null && a.AdmissionNumber.StartsWith(prefix)).Select(a => a.AdmissionNumber!))
            .ToListAsync(ct);
        var max = numbers.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
        return Student.AdmissionNumberFor(year, max + 1);
    }

    /// <summary>A class can take another active student (excluding <paramref name="exceptStudentId"/>), or the error to return.</summary>
    public static async Task<Common.Error?> CheckClassHasRoomAsync(this IAppDbContext db, Guid classRoomId, Guid? exceptStudentId, CancellationToken ct)
    {
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == classRoomId)
            .Select(r => new { Name = r.ClassLevel.Name + " " + r.Arm, r.Capacity }).FirstOrDefaultAsync(ct);
        if (room is null) return StudentErrors.NoClass;
        var enrolled = await db.Students.CountAsync(s => s.ClassRoomId == classRoomId && s.Status == StudentStatus.Active && s.Id != exceptStudentId, ct);
        return enrolled >= room.Capacity ? StudentErrors.ClassFull(room.Name, room.Capacity) : null;
    }
}

public static class StudentLogins
{
    /// <summary>Turns the student's own login on or off to match their status; turning it off signs them out everywhere at once.</summary>
    public static async Task SyncStudentLoginAsync(this Microsoft.AspNetCore.Identity.UserManager<AppUser> users, Student student, SessionService sessions, CancellationToken ct)
    {
        if (student.UserId is not { } userId || await users.FindByIdAsync(userId.ToString()) is not { } user || user.IsActive == student.IsActive) return;
        user.IsActive = student.IsActive;
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
        if (!student.IsActive) await sessions.RevokeAllForUserAsync(user.Id, ct);
    }
}

/// <summary>Shared by the student and guardian login slices.</summary>
public static class LoginIssuing
{
    /// <summary>New temporary password on an existing account: unlock, must-change, signed out everywhere.</summary>
    public static async Task<IssuedCredentials> ResetToTemporaryPasswordAsync(this Microsoft.AspNetCore.Identity.UserManager<AppUser> users, AppUser user, SessionService sessions, CancellationToken ct)
    {
        var password = TemporaryPassword.Generate();
        if (await users.HasPasswordAsync(user)) await users.RemovePasswordAsync(user);
        var added = await users.AddPasswordAsync(user, password);
        if (!added.Succeeded) throw new InvalidOperationException("Could not set temporary password: " + string.Join("; ", added.Errors.Select(e => e.Code)));
        user.MustChangePassword = true;
        user.IsActive = true;
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
        await sessions.RevokeAllForUserAsync(user.Id, ct);
        return new IssuedCredentials(user.UserName!, password);
    }
}
