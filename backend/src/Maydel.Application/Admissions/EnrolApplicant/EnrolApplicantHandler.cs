using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.Students;
using Maydel.Domain.Admissions;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Admissions.EnrolApplicant;

internal sealed class EnrolApplicantHandler(IAppDbContext db, UserManager<AppUser> users, ISchoolClock clock, AdmissionEmails emails)
    : ICommandHandler<EnrolApplicantCommand, EnrolmentResult>
{
    public async Task<Result<EnrolmentResult>> Handle(EnrolApplicantCommand c, CancellationToken ct)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == c.ApplicationId, ct);
        if (application is null) return AdmissionErrors.ApplicationNotFound;
        if (application.Status != ApplicationStatus.Accepted || application.AdmissionNumber is not { } number)
            return Error.Rule("admission.not_accepted", "Only accepted offers can be enrolled.");
        if (await db.Students.AnyAsync(s => s.AdmissionNumber == number, ct))
            return Error.Conflict("admission.number_taken", $"A student already has admission number {number}.");

        string? classRoom = null;
        if (c.ClassRoomId is { } roomId)
        {
            var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => new { r.ClassLevelId, Name = r.ClassLevel.Name + " " + r.Arm }).FirstOrDefaultAsync(ct);
            if (room is null || room.ClassLevelId != application.ClassLevelId)
                return Error.Validation("classRoomId", "Choose a class for the level the child was offered.");
            if (await db.CheckClassHasRoomAsync(roomId, null, ct) is { } full) return full;
            classRoom = room.Name;
        }

        // Admitted from the start of the session (or today, if it has started) so the first invoice bills new-intake items.
        var today = clock.Today;
        var sessionStart = await db.Sessions.Where(s => s.Id == application.SessionId).Select(s => s.StartsOn).FirstAsync(ct);
        var admittedOn = sessionStart > today ? sessionStart : today;
        if (Student.ValidateDates(application.ChildDateOfBirth, admittedOn, today) is { } invalid) return invalid.ToError();

        var f = application.Form;
        var student = new Student
        {
            AdmissionNumber = number, FirstName = application.ChildFirstName, MiddleName = f.ChildMiddleName, LastName = application.ChildLastName,
            DateOfBirth = application.ChildDateOfBirth, Sex = f.ChildSex, StateOfOrigin = f.StateOfOrigin, Lga = f.Lga, HomeAddress = f.HomeAddress,
            MedicalNotes = string.Join("\n", new[] { f.MedicalNotes, f.SpecialNeeds is { } needs ? $"Special needs: {needs}" : null }.Where(x => x is not null)) is { Length: > 0 } notes ? notes : null,
            ClassRoomId = c.ClassRoomId, AdmittedOn = admittedOn,
        };
        db.Students.Add(student);

        // Guardians: father, mother and the person who applied (often one of them). Same phone = same guardian.
        var applicantPhone = Guardian.NormalizePhone(application.Phone);
        var people = new List<(string Name, string? Phone, string? Email, string? Occupation, GuardianRelationship Relationship)>
        {
            (f.FatherName ?? "", f.FatherPhone, f.FatherEmail, f.FatherOccupation, GuardianRelationship.Father),
            (f.MotherName ?? "", f.MotherPhone, f.MotherEmail, f.MotherOccupation, GuardianRelationship.Mother),
            (application.ParentName, application.Phone, application.Email, null, GuardianRelationship.Guardian),
        };
        var linked = new Dictionary<string, Guardian>();
        Guardian? applicant = null;
        foreach (var (name, rawPhone, email, occupation, relationship) in people)
        {
            if (string.IsNullOrWhiteSpace(name) || Guardian.NormalizePhone(rawPhone) is not { } phone) continue;
            var isApplicant = phone == applicantPhone || (email is not null && string.Equals(email, application.Email, StringComparison.OrdinalIgnoreCase));
            if (linked.TryGetValue(phone, out var already))
            {
                if (isApplicant) applicant ??= already;
                continue;
            }
            var guardian = await db.Guardians.FirstOrDefaultAsync(g => g.Phone == phone, ct);
            if (guardian is null)
            {
                guardian = new Guardian
                {
                    FullName = name.Trim(), Phone = phone, Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
                    Occupation = occupation, Address = f.HomeAddress,
                };
                db.Guardians.Add(guardian);
            }
            db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = student.Id, GuardianId = guardian.Id, Relationship = relationship, IsPrimaryContact = false, CanPickUp = true,
            });
            linked[phone] = guardian;
            if (isApplicant) applicant ??= guardian;
        }
        applicant ??= linked.Values.FirstOrDefault();
        if (applicant is not null)
            foreach (var link in db.StudentGuardians.Local.Where(l => l.StudentId == student.Id))
                link.IsPrimaryContact = link.GuardianId == applicant.Id;

        // The applicant login becomes the parent login (same email and password).
        var portalReady = false;
        if (applicant is not null && await users.FindByIdAsync(application.ApplicantUserId.ToString()) is { } user)
        {
            var linkedElsewhere = await db.Guardians.AnyAsync(g => g.UserId == user.Id && g.Id != applicant.Id, ct);
            if (applicant.UserId is null && !linkedElsewhere) applicant.UserId = user.Id;
            if (applicant.UserId == user.Id || linkedElsewhere)
            {
                if (!await users.IsInRoleAsync(user, Roles.Parent)) await users.AddToRoleAsync(user, Roles.Parent);
                await users.UpdateSecurityStampAsync(user);
                portalReady = true;
            }
        }

        var now = clock.Now.ToUniversalTime();
        application.Enrol(student.Id, now);
        await emails.QueueAsync(application, "Enrolment complete",
            $"{application.ChildName} is now enrolled{(classRoom is null ? "" : $" in {classRoom}")} with admission number {number}. " +
            (portalReady
                ? "Sign in with your usual email and password to open the parent portal, where you can see fees, pay online and view results."
                : "The school will send your parent portal login separately."), ct);
        await db.SaveChangesAsync(ct);
        return new EnrolmentResult(student.Id, number, (await db.DetailAsync(application.Id, ct, asStaff: true))!);
    }
}
