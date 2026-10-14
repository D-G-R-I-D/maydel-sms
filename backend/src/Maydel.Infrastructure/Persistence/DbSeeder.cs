using Maydel.Domain.Academics;
using Maydel.Domain.Assessment;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.People;
using Maydel.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Bootstrap administrator. Required on first run; change the password after first login.</summary>
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }

    /// <summary>Adds sample staff, students and guardians. Never enable in production.</summary>
    public bool DemoData { get; set; }
}

/// <summary>Idempotent reference-data seeding. Safe to run on every start-up.</summary>
public sealed class DbSeeder(
    AppDbContext db,
    UserManager<AppUser> users,
    RoleManager<AppRole> roles,
    IOptions<SeedOptions> options,
    ILogger<DbSeeder> logger)
{
    private readonly SeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new AppRole(role));

        await SeedAdminAsync();
        await SeedAcademicStructureAsync(ct);
        await SeedAssessmentAsync(ct);
        if (_options.DemoData)
        {
            await SeedDemoDataAsync(ct);
            await ResetDemoAccountsAsync();
        }
    }

    private async Task SeedAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.AdminEmail) || string.IsNullOrWhiteSpace(_options.AdminPassword)) return;
        if (await users.FindByEmailAsync(_options.AdminEmail) is not null) return;

        var admin = new AppUser
        {
            UserName = _options.AdminEmail,
            Email = _options.AdminEmail,
            EmailConfirmed = true,
            FullName = "School Administrator",
        };
        var result = await users.CreateAsync(admin, _options.AdminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not create bootstrap admin: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Bootstrap admin {Email} created", _options.AdminEmail);
    }

    private async Task SeedAcademicStructureAsync(CancellationToken ct)
    {
        if (!await db.ClassLevels.AnyAsync(ct))
        {
            var levels = new List<(string Name, SchoolSection Section)>
            {
                ("Creche", SchoolSection.Creche),
                ("Nursery 1", SchoolSection.Nursery),
                ("Nursery 2", SchoolSection.Nursery),
            };
            for (var y = 1; y <= 6; y++) levels.Add(($"Year {y}", SchoolSection.Primary));
            for (var y = 7; y <= 9; y++) levels.Add(($"Year {y}", SchoolSection.JuniorSecondary));
            for (var y = 10; y <= 12; y++) levels.Add(($"Year {y}", SchoolSection.SeniorSecondary));

            var order = 0;
            foreach (var (name, section) in levels)
            {
                var level = new ClassLevel { Name = name, Section = section, Order = ++order };
                level.ClassRooms.Add(new ClassRoom { Arm = "A" });
                db.ClassLevels.Add(level);
            }
        }

        if (!await db.Subjects.AnyAsync(ct))
        {
            // Mirrors the subjects on the school's existing result template.
            (string Name, string Code)[] subjects =
            [
                ("English Language", "ENG"), ("Mathematics", "MTH"), ("ICT", "ICT"), ("Basic Science", "BSC"),
                ("Creative & Cultural Arts", "CCA"), ("History", "HIS"), ("National Values", "NVE"),
                ("Pre-Vocational Studies", "PVS"), ("French", "FRE"),
            ];
            db.Subjects.AddRange(subjects.Select(s => new Subject { Name = s.Name, Code = s.Code }));
        }

        if (!await db.Sessions.AnyAsync(ct))
        {
            var session = new AcademicSession
            {
                Name = "2026/2027",
                StartsOn = new DateOnly(2026, 9, 14),
                EndsOn = new DateOnly(2027, 7, 23),
                IsCurrent = true,
            };
            session.Terms.Add(new Term { Number = TermNumber.First, StartsOn = new(2026, 9, 14), EndsOn = new(2026, 12, 11), IsCurrent = true });
            session.Terms.Add(new Term { Number = TermNumber.Second, StartsOn = new(2027, 1, 11), EndsOn = new(2027, 3, 26) });
            session.Terms.Add(new Term { Number = TermNumber.Third, StartsOn = new(2027, 4, 19), EndsOn = new(2027, 7, 23) });
            db.Sessions.Add(session);
        }

        await db.SaveChangesAsync(ct);

        // Every class room offers every subject by default; admins can prune per class.
        if (!await db.ClassSubjects.AnyAsync(ct))
        {
            var rooms = await db.ClassRooms.Select(r => r.Id).ToListAsync(ct);
            var subjectIds = await db.Subjects.Select(s => s.Id).ToListAsync(ct);
            db.ClassSubjects.AddRange(rooms.SelectMany(r => subjectIds.Select(s => new ClassSubject { ClassRoomId = r, SubjectId = s })));
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task SeedAssessmentAsync(CancellationToken ct)
    {
        if (!await db.AssessmentComponents.AnyAsync(ct))
        {
            // The legacy template summed to 105; the school reduced Class Fun from 10 to 5 (2026-10-12).
            // CAT = Continuous Assessment Test. Continuous assessment 60 + Exams 40 = 100.
            (string Name, decimal Max)[] components =
                [("Project", 10), ("Homefun", 5), ("Class Fun", 5), ("Attendance", 10), ("CAT", 30), ("Exams", 40)];
            var order = 0;
            db.AssessmentComponents.AddRange(components.Select(c => new AssessmentComponent { Name = c.Name, MaxScore = c.Max, Order = ++order }));
        }

        if (!await db.GradeBands.AnyAsync(ct))
        {
            // The school's own 8-grade scale (agreed 2026-10-14); "Simple (6 grades)" stays available as a preset.
            db.GradeBands.AddRange(GradingPresets.SchoolStandard.Bands.Select(b =>
                new GradeBand { Grade = b.Grade, MinScore = b.MinScore, MaxScore = b.MaxScore, Remark = b.Remark }));
        }

        await db.SaveChangesAsync(ct);
    }

    private const string DemoPassword = "Demo#Pass2026";

    /// <summary>
    /// Demo accounts and whether each must change its password at first sign-in. Restored on every start-up (demo data
    /// only, never production) so e2e/smoke runs and manual testing always find them in a known state.
    /// </summary>
    private static readonly (string UserName, bool MustChangePassword)[] DemoAccounts =
    [
        ("admin@maydel.test", false), ("teacher@maydel.test", false), ("bursar@maydel.test", false),
        ("parent@maydel.test", false), ("newteacher@maydel.test", true),
        ("MSS/2026/0001", false), ("MSS/2026/0002", false), ("MSS/2026/0003", false), ("MSS/2026/0004", false), ("MSS/2026/0005", false),
    ];

    private async Task ResetDemoAccountsAsync()
    {
        foreach (var (userName, mustChange) in DemoAccounts)
        {
            if (await users.FindByNameAsync(userName) is not { } user) continue;
            if (!await users.CheckPasswordAsync(user, DemoPassword))
            {
                if (await users.HasPasswordAsync(user)) await users.RemovePasswordAsync(user);
                await users.AddPasswordAsync(user, DemoPassword);
            }
            user.MustChangePassword = mustChange;
            user.IsActive = true;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await users.UpdateAsync(user);
        }
    }

    private async Task SeedDemoDataAsync(CancellationToken ct)
    {
        if (await db.Students.AnyAsync(ct)) return;
        const string demoPassword = DemoPassword;

        var teacherUser = await CreateUserAsync("teacher@maydel.test", "Mrs. Grace Okafor", Roles.Teacher, demoPassword);
        var teacher = new Staff
        {
            StaffNumber = "MSS-STF-001", FullName = "Mrs. Grace Okafor", Email = "teacher@maydel.test",
            JobTitle = "Year 3 Class Teacher", UserId = teacherUser.Id, ShowOnWebsite = true,
        };
        db.Staff.Add(teacher);
        await CreateUserAsync("bursar@maydel.test", "Mr. Tunde Bello", Roles.Bursar, demoPassword);

        // Exercises the first-login flow (E1.4): signs in with the demo password, then must choose a new one.
        var newTeacher = await CreateUserAsync("newteacher@maydel.test", "Mr. Samuel Adeyemi", Roles.Teacher, demoPassword);
        newTeacher.MustChangePassword = true;
        await users.UpdateAsync(newTeacher);

        var year3 = await db.ClassRooms.Include(c => c.ClassLevel).FirstAsync(c => c.ClassLevel.Name == "Year 3", ct);
        year3.FormTeacher = teacher;
        await db.ClassSubjects.Where(cs => cs.ClassRoomId == year3.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.TeacherId, teacher.Id), ct);

        var parentUser = await CreateUserAsync("parent@maydel.test", "Mrs. Ada Eze", Roles.Parent, demoPassword);
        var guardian = new Guardian { FullName = "Mrs. Ada Eze", Phone = "+2348030000000", Email = "parent@maydel.test", UserId = parentUser.Id };

        string[][] names = [["Chidera", "Eze"], ["Kamsi", "Eze"], ["Aisha", "Bello"], ["David", "Okon"], ["Zainab", "Musa"]];
        var n = 0;
        foreach (var name in names)
        {
            var admissionNo = $"MSS/2026/{++n:0000}";
            var studentUser = await CreateUserAsync(admissionNo, $"{name[0]} {name[1]}", Roles.Student, demoPassword, email: null);
            var student = new Student
            {
                AdmissionNumber = admissionNo, FirstName = name[0], LastName = name[1],
                ClassRoomId = year3.Id, AdmittedOn = new DateOnly(2026, 9, 14), UserId = studentUser.Id,
                Sex = n % 2 == 0 ? Sex.Male : Sex.Female, DateOfBirth = new DateOnly(2018, n, 10),
            };
            if (name[1] == "Eze")
                student.Guardians.Add(new StudentGuardian { Guardian = guardian, Relationship = GuardianRelationship.Mother, IsPrimaryContact = true });
            db.Students.Add(student);
        }

        db.Announcements.Add(new Announcement
        {
            Title = "New Academic Session Begins",
            Body = "The 2026/2027 academic session commences with a special orientation programme for all new and returning students.",
            Category = "Announcement", PublishedAt = DateTimeOffset.UtcNow, Audience = Audience.Everyone, IsPinned = true,
        });
        db.Announcements.Add(new Announcement
        {
            Title = "WAEC & NECO Approval Confirmed",
            Body = "Maydel Spring Schools is officially approved as an examination centre for both WAEC and NECO.",
            Category = "Milestone", PublishedAt = DateTimeOffset.UtcNow.AddDays(-30), Audience = Audience.Everyone,
        });

        await db.SaveChangesAsync(ct);
        logger.LogWarning("Demo data seeded. Demo accounts use the password {Password}", demoPassword);
    }

    private async Task<AppUser> CreateUserAsync(string userName, string fullName, string role, string password, string? email = "")
    {
        var user = new AppUser
        {
            UserName = userName,
            Email = email == "" ? userName : email,
            EmailConfirmed = true,
            FullName = fullName,
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        return user;
    }
}
