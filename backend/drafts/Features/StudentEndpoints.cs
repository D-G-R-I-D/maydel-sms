using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Maydel.Api.Auth;
using Maydel.Api.Common;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Maydel.Infrastructure.Identity;
using Maydel.Infrastructure.Persistence;
using Maydel.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Api.Features;

public sealed record StudentListItem(Guid Id, string AdmissionNumber, string FullName, Sex? Sex, Guid? ClassRoomId, string? ClassRoom, StudentStatus Status, bool HasLogin, bool HasPhoto);

public sealed record GuardianDto(Guid Id, string FullName, string Phone, string? Email, GuardianRelationship Relationship, bool IsPrimaryContact, bool CanPickUp, bool HasLogin);

public sealed record StudentDetail(
    Guid Id, string AdmissionNumber, string FirstName, string? MiddleName, string LastName, string FullName,
    DateOnly? DateOfBirth, Sex? Sex, string? StateOfOrigin, string? Lga, string? HomeAddress, string? House,
    string? MedicalNotes, DateOnly AdmittedOn, StudentStatus Status, Guid? ClassRoomId, string? ClassRoom,
    bool HasLogin, bool HasPhoto, IReadOnlyList<GuardianDto> Guardians);

public sealed record UpsertStudentRequest(
    [property: Required, MaxLength(80)] string FirstName,
    [property: MaxLength(80)] string? MiddleName,
    [property: Required, MaxLength(80)] string LastName,
    DateOnly? DateOfBirth,
    Sex? Sex,
    [property: MaxLength(80)] string? StateOfOrigin,
    [property: MaxLength(80)] string? Lga,
    [property: MaxLength(1000)] string? HomeAddress,
    [property: MaxLength(80)] string? House,
    [property: MaxLength(2000)] string? MedicalNotes,
    Guid? ClassRoomId,
    DateOnly? AdmittedOn,
    StudentStatus Status = StudentStatus.Active);

public sealed record AddGuardianRequest(
    Guid? ExistingGuardianId,
    [property: MaxLength(150)] string? FullName,
    [property: Phone, MaxLength(32)] string? Phone,
    [property: EmailAddress, MaxLength(256)] string? Email,
    GuardianRelationship Relationship,
    bool IsPrimaryContact,
    bool CanPickUp = true);

public sealed record CredentialsResponse(string UserName, string TemporaryPassword);

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/students").WithTags("Students").RequireAuthorization(Policies.Staff);
        var admin = g.MapGroup("").RequireAuthorization(Policies.Admin);

        g.MapGet("", async (AppDbContext db, string? search, Guid? classRoomId, StudentStatus? status, int? page, int? pageSize, CancellationToken ct) =>
        {
            var q = db.Students.AsNoTracking();
            if (classRoomId is not null) q = q.Where(s => s.ClassRoomId == classRoomId);
            q = q.Where(s => s.Status == (status ?? StudentStatus.Active));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                q = q.Where(s => EF.Functions.ILike(s.FirstName + " " + s.LastName, term)
                                 || EF.Functions.ILike(s.LastName + " " + s.FirstName, term)
                                 || EF.Functions.ILike(s.AdmissionNumber, term));
            }
            return await q.OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .Select(s => new StudentListItem(s.Id, s.AdmissionNumber, s.FirstName + " " + s.LastName, s.Sex, s.ClassRoomId,
                    s.ClassRoom != null ? s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm : null, s.Status, s.UserId != null, s.PhotoPath != null))
                .ToPagedAsync(page, pageSize, ct);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
            await LoadDetailAsync(db, id, ct) is { } d ? Results.Ok(d) : Problems.NotFound());

        admin.MapPost("", async (UpsertStudentRequest req, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var student = new Student
            {
                AdmissionNumber = await NextAdmissionNumberAsync(db, clock, ct),
                FirstName = req.FirstName.Trim(),
                LastName = req.LastName.Trim(),
                AdmittedOn = req.AdmittedOn ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
            };
            Apply(student, req);
            db.Students.Add(student);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/students/{student.Id}", await LoadDetailAsync(db, student.Id, ct));
        });

        admin.MapPut("/{id:guid}", async (Guid id, UpsertStudentRequest req, AppDbContext db, UserManager<AppUser> users, CancellationToken ct) =>
        {
            var student = await db.Students.FindAsync([id], ct);
            if (student is null) return Problems.NotFound();
            Apply(student, req);
            await db.SaveChangesAsync(ct);

            // Leaving the school disables the student's login.
            if (student.UserId is { } uid && await users.FindByIdAsync(uid.ToString()) is { } user)
            {
                user.IsActive = student.Status is StudentStatus.Active or StudentStatus.Suspended;
                await users.UpdateAsync(user);
                if (!user.IsActive) await users.UpdateSecurityStampAsync(user);
            }
            return Results.Ok(await LoadDetailAsync(db, id, ct));
        });

        // Issues (or resets) the student's own login. The temporary password is shown once and must be changed on first sign-in.
        admin.MapPost("/{id:guid}/credentials", async (Guid id, AppDbContext db, UserManager<AppUser> users, TokenService tokens, CancellationToken ct) =>
        {
            var student = await db.Students.FindAsync([id], ct);
            if (student is null) return Problems.NotFound();
            var password = TemporaryPassword();

            if (student.UserId is { } uid && await users.FindByIdAsync(uid.ToString()) is { } existing)
            {
                await users.RemovePasswordAsync(existing);
                var reset = await users.AddPasswordAsync(existing, password);
                if (!reset.Succeeded) return Problems.Validation(reset);
                existing.MustChangePassword = true;
                existing.IsActive = true;
                await users.SetLockoutEndDateAsync(existing, null);
                await users.UpdateAsync(existing);
                await users.UpdateSecurityStampAsync(existing);
                await tokens.RevokeAllForUserAsync(existing.Id);
                return Results.Ok(new CredentialsResponse(existing.UserName!, password));
            }

            var user = new AppUser { UserName = student.AdmissionNumber, FullName = student.FullName, MustChangePassword = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded) return Problems.Validation(created);
            await users.AddToRoleAsync(user, Roles.Student);
            student.UserId = user.Id;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CredentialsResponse(user.UserName!, password));
        });

        admin.MapPost("/{id:guid}/guardians", async (Guid id, AddGuardianRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var student = await db.Students.Include(s => s.Guardians).FirstOrDefaultAsync(s => s.Id == id, ct);
            if (student is null) return Problems.NotFound();

            Guardian? guardian;
            if (req.ExistingGuardianId is { } gid)
            {
                guardian = await db.Guardians.FindAsync([gid], ct);
                if (guardian is null) return Problems.Validation("existingGuardianId", "Unknown guardian.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Phone))
                    return Problems.Validation("fullName", "Name and phone are required for a new guardian.");
                // Re-use a guardian with the same phone so siblings share one parent account.
                guardian = await db.Guardians.FirstOrDefaultAsync(x => x.Phone == req.Phone.Trim(), ct)
                           ?? new Guardian { FullName = req.FullName.Trim(), Phone = req.Phone.Trim(), Email = req.Email?.Trim() };
            }

            if (student.Guardians.Any(sg => sg.GuardianId == guardian.Id)) return Problems.Conflict("This guardian is already linked.");
            if (req.IsPrimaryContact) student.Guardians.ForEach(sg => sg.IsPrimaryContact = false);
            student.Guardians.Add(new StudentGuardian { Guardian = guardian, Relationship = req.Relationship, IsPrimaryContact = req.IsPrimaryContact, CanPickUp = req.CanPickUp });
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadDetailAsync(db, id, ct));
        });

        admin.MapDelete("/{id:guid}/guardians/{guardianId:guid}", async (Guid id, Guid guardianId, AppDbContext db, CancellationToken ct) =>
        {
            var deleted = await db.StudentGuardians.Where(sg => sg.StudentId == id && sg.GuardianId == guardianId).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Problems.NotFound() : Results.NoContent();
        });

        // Parent login: email + temporary password. Parents can also link Google with the same email.
        admin.MapPost("/guardians/{guardianId:guid}/credentials", async (Guid guardianId, AppDbContext db, UserManager<AppUser> users, CancellationToken ct) =>
        {
            var guardian = await db.Guardians.FindAsync([guardianId], ct);
            if (guardian is null) return Problems.NotFound();
            if (string.IsNullOrWhiteSpace(guardian.Email)) return Problems.Rule("Add an email address for this guardian before creating a login.");
            var password = TemporaryPassword();

            var user = guardian.UserId is { } uid ? await users.FindByIdAsync(uid.ToString()) : await users.FindByEmailAsync(guardian.Email);
            if (user is null)
            {
                user = new AppUser { UserName = guardian.Email, Email = guardian.Email, EmailConfirmed = true, FullName = guardian.FullName, MustChangePassword = true };
                var created = await users.CreateAsync(user, password);
                if (!created.Succeeded) return Problems.Validation(created);
                await users.AddToRoleAsync(user, Roles.Parent);
            }
            else
            {
                if (!await users.IsInRoleAsync(user, Roles.Parent)) return Problems.Conflict("This email already belongs to a non-parent account.");
                await users.RemovePasswordAsync(user);
                await users.AddPasswordAsync(user, password);
                user.MustChangePassword = true;
                await users.UpdateAsync(user);
                await users.UpdateSecurityStampAsync(user);
            }
            guardian.UserId = user.Id;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CredentialsResponse(user.UserName!, password));
        });

        // ----- Photos (personal data: never served from a public folder) -----
        admin.MapPost("/{id:guid}/photo", async (Guid id, IFormFile file, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var student = await db.Students.FindAsync([id], ct);
            if (student is null) return Problems.NotFound();
            var (ext, error) = await Files.ValidateImageAsync(file);
            if (error is not null) return Problems.Validation("file", error);

            await using var stream = file.OpenReadStream();
            var old = student.PhotoPath;
            student.PhotoPath = await storage.SaveAsync(stream, "students", ext!, ct);
            await db.SaveChangesAsync(ct);
            if (old is not null) await storage.DeleteAsync(old, ct);
            return Results.NoContent();
        }).DisableAntiforgery();

        app.MapGet("/api/students/{id:guid}/photo", async (Guid id, AppDbContext db, IFileStorage storage, CurrentUser me, CancellationToken ct) =>
        {
            if (!await Access.CanViewStudentAsync(db, me, id, ct)) return Problems.Forbidden();
            var path = await db.Students.Where(s => s.Id == id).Select(s => s.PhotoPath).FirstOrDefaultAsync(ct);
            var stream = path is null ? null : await storage.OpenAsync(path, ct);
            return stream is null ? Problems.NotFound() : Results.File(stream, path!.EndsWith(".png") ? "image/png" : path.EndsWith(".webp") ? "image/webp" : "image/jpeg");
        }).RequireAuthorization().WithTags("Students");
    }

    private static void Apply(Student s, UpsertStudentRequest r)
    {
        s.FirstName = r.FirstName.Trim();
        s.MiddleName = r.MiddleName?.Trim();
        s.LastName = r.LastName.Trim();
        s.DateOfBirth = r.DateOfBirth;
        s.Sex = r.Sex;
        s.StateOfOrigin = r.StateOfOrigin;
        s.Lga = r.Lga;
        s.HomeAddress = r.HomeAddress;
        s.House = r.House;
        s.MedicalNotes = r.MedicalNotes;
        s.ClassRoomId = r.ClassRoomId;
        s.Status = r.Status;
        if (r.AdmittedOn is { } d) s.AdmittedOn = d;
    }

    private static async Task<string> NextAdmissionNumberAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var prefix = $"MSS/{clock.GetUtcNow().Year}/";
        var last = await db.Students.Where(s => s.AdmissionNumber.StartsWith(prefix))
            .OrderByDescending(s => s.AdmissionNumber).Select(s => s.AdmissionNumber).FirstOrDefaultAsync(ct);
        var next = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return $"{prefix}{next:0000}";
    }

    /// <summary>Readable but strong: no ambiguous characters, satisfies the password policy.</summary>
    internal static string TemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz", digits = "23456789";
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++)
        {
            var set = (i % 3) switch { 0 => upper, 1 => lower, _ => digits };
            chars[i] = set[RandomNumberGenerator.GetInt32(set.Length)];
        }
        return new string(chars);
    }

    internal static async Task<StudentDetail?> LoadDetailAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var s = await db.Students.AsNoTracking()
            .Include(x => x.ClassRoom).ThenInclude(c => c!.ClassLevel)
            .Include(x => x.Guardians).ThenInclude(g => g.Guardian)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;
        return new StudentDetail(
            s.Id, s.AdmissionNumber, s.FirstName, s.MiddleName, s.LastName, s.FullName, s.DateOfBirth, s.Sex, s.StateOfOrigin, s.Lga,
            s.HomeAddress, s.House, s.MedicalNotes, s.AdmittedOn, s.Status, s.ClassRoomId, s.ClassRoom?.DisplayName,
            s.UserId is not null, s.PhotoPath is not null,
            s.Guardians.OrderByDescending(g => g.IsPrimaryContact).Select(g => new GuardianDto(
                g.GuardianId, g.Guardian.FullName, g.Guardian.Phone, g.Guardian.Email, g.Relationship, g.IsPrimaryContact, g.CanPickUp, g.Guardian.UserId is not null)).ToList());
    }
}

/// <summary>Row-level access rules shared across features.</summary>
public static class Access
{
    /// <summary>Staff see all students; parents only their linked children; students only themselves.</summary>
    public static async Task<bool> CanViewStudentAsync(AppDbContext db, CurrentUser me, Guid studentId, CancellationToken ct)
    {
        if (me.IsInRole(Roles.Admin) || me.IsInRole(Roles.Teacher) || me.IsInRole(Roles.Bursar)) return true;
        var uid = me.UserId;
        if (uid is null) return false;
        if (me.IsInRole(Roles.Student) && await db.Students.AnyAsync(s => s.Id == studentId && s.UserId == uid, ct)) return true;
        return me.IsInRole(Roles.Parent) && await db.StudentGuardians.AnyAsync(sg => sg.StudentId == studentId && sg.Guardian.UserId == uid, ct);
    }

    /// <summary>Students the signed-in parent or student may see in the portal.</summary>
    public static IQueryable<Student> PortalStudents(AppDbContext db, CurrentUser me)
    {
        var uid = me.RequiredUserId;
        return db.Students.Where(s => s.UserId == uid || s.Guardians.Any(g => g.Guardian.UserId == uid));
    }

    /// <summary>Teachers may only grade subjects they teach, or any subject in a class they form-teach.</summary>
    public static async Task<bool> CanGradeAsync(AppDbContext db, CurrentUser me, Guid classRoomId, Guid subjectId, CancellationToken ct)
    {
        if (me.IsInRole(Roles.Admin)) return true;
        if (!me.IsInRole(Roles.Teacher) || me.UserId is not { } uid) return false;
        return await db.ClassSubjects.AnyAsync(cs => cs.ClassRoomId == classRoomId && cs.SubjectId == subjectId &&
                   ((cs.Teacher != null && cs.Teacher.UserId == uid) || (cs.ClassRoom.FormTeacher != null && cs.ClassRoom.FormTeacher.UserId == uid)), ct);
    }

    public static async Task<bool> IsFormTeacherAsync(AppDbContext db, CurrentUser me, Guid classRoomId, CancellationToken ct)
    {
        if (me.IsInRole(Roles.Admin)) return true;
        return me.UserId is { } uid && await db.ClassRooms.AnyAsync(c => c.Id == classRoomId && c.FormTeacher != null && c.FormTeacher.UserId == uid, ct);
    }
}
