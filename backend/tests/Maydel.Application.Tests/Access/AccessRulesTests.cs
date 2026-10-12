using Maydel.Application.Common.Access;
using Maydel.Application.Tests.Infrastructure;
using Maydel.Domain.Academics;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Tests.Access;

/// <summary>Row-level rules from docs/04-security.md §3 (E1.6), against a real database.</summary>
[Collection(nameof(DatabaseCollection))]
public sealed class AccessRulesTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Parent_can_view_own_child_but_not_another_familys_child()
    {
        var school = await SeedSchoolAsync();
        var rules = Rules(school.ParentUserId, Roles.Parent);

        Assert.True(await rules.CanViewStudentAsync(school.OwnChildId, default));
        Assert.False(await rules.CanViewStudentAsync(school.OtherChildId, default));
    }

    [Fact]
    public async Task Parent_portal_lists_only_their_children()
    {
        var school = await SeedSchoolAsync();
        var ids = await Rules(school.ParentUserId, Roles.Parent).PortalStudents().Select(s => s.Id).ToListAsync();

        Assert.Equal([school.OwnChildId], ids);
    }

    [Fact]
    public async Task Student_can_view_only_themselves()
    {
        var school = await SeedSchoolAsync();
        var rules = Rules(school.OwnChildUserId, Roles.Student);

        Assert.True(await rules.CanViewStudentAsync(school.OwnChildId, default));
        Assert.False(await rules.CanViewStudentAsync(school.OtherChildId, default));
    }

    [Fact]
    public async Task A_parent_role_does_not_grant_access_through_a_student_login_and_vice_versa()
    {
        var school = await SeedSchoolAsync();

        // The child's own login id, but claiming only the Parent role, must not match the "self" rule.
        Assert.False(await Rules(school.OwnChildUserId, Roles.Parent).CanViewStudentAsync(school.OwnChildId, default));
        Assert.False(await Rules(school.ParentUserId, Roles.Student).CanViewStudentAsync(school.OwnChildId, default));
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Bursar)]
    public async Task Staff_can_view_any_student(string role)
    {
        var school = await SeedSchoolAsync();
        Assert.True(await Rules(Guid.NewGuid(), role).CanViewStudentAsync(school.OtherChildId, default));
    }

    [Fact]
    public async Task Anonymous_caller_can_view_nothing()
    {
        var school = await SeedSchoolAsync();
        var rules = new AccessRules(fixture.CreateContext(new TestUser()), new TestUser());

        Assert.False(await rules.CanViewStudentAsync(school.OwnChildId, default));
        Assert.Empty(await rules.PortalStudents().ToListAsync());
    }

    [Fact]
    public async Task Subject_teacher_can_grade_only_their_assigned_subject_and_class()
    {
        var school = await SeedSchoolAsync();
        var rules = Rules(school.SubjectTeacherUserId, Roles.Teacher);

        Assert.True(await rules.CanGradeAsync(school.ClassA, school.Maths, default));
        Assert.False(await rules.CanGradeAsync(school.ClassA, school.English, default));
        Assert.False(await rules.CanGradeAsync(school.ClassB, school.Maths, default));
    }

    [Fact]
    public async Task Form_teacher_can_grade_every_subject_in_their_form_class_only()
    {
        var school = await SeedSchoolAsync();
        var rules = Rules(school.FormTeacherUserId, Roles.Teacher);

        Assert.True(await rules.CanGradeAsync(school.ClassA, school.Maths, default));
        Assert.True(await rules.CanGradeAsync(school.ClassA, school.English, default));
        Assert.False(await rules.CanGradeAsync(school.ClassB, school.English, default));
        Assert.True(await rules.IsFormTeacherAsync(school.ClassA, default));
        Assert.False(await rules.IsFormTeacherAsync(school.ClassB, default));
    }

    [Theory]
    [InlineData(Roles.Bursar)]
    [InlineData(Roles.Parent)]
    [InlineData(Roles.Student)]
    public async Task Non_teaching_roles_cannot_grade(string role)
    {
        var school = await SeedSchoolAsync();
        var rules = Rules(school.SubjectTeacherUserId, role);

        Assert.False(await rules.CanGradeAsync(school.ClassA, school.Maths, default));
        Assert.False(await rules.IsFormTeacherAsync(school.ClassA, default));
    }

    [Fact]
    public async Task Admin_can_grade_anything()
    {
        var school = await SeedSchoolAsync();
        Assert.True(await Rules(Guid.NewGuid(), Roles.Admin).CanGradeAsync(school.ClassB, school.English, default));
    }

    private AccessRules Rules(Guid userId, string role)
    {
        var user = new TestUser(userId, role);
        return new AccessRules(fixture.CreateContext(user), user);
    }

    private sealed record School(
        Guid ParentUserId, Guid OwnChildId, Guid OwnChildUserId, Guid OtherChildId,
        Guid SubjectTeacherUserId, Guid FormTeacherUserId, Guid ClassA, Guid ClassB, Guid Maths, Guid English);

    /// <summary>Two classes, two subjects, a subject teacher (Maths in class A), a form teacher (class A), two families.</summary>
    private async Task<School> SeedSchoolAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await using var db = fixture.CreateContext(new TestUser());

        var level = new ClassLevel { Name = $"Level {tag}", Section = SchoolSection.Primary, Order = 1 };
        var classA = new ClassRoom { ClassLevel = level, Arm = "A" };
        var classB = new ClassRoom { ClassLevel = level, Arm = "B" };
        var maths = new Subject { Name = $"Maths {tag}", Code = $"M{tag}" };
        var english = new Subject { Name = $"English {tag}", Code = $"E{tag}" };

        var subjectTeacher = new Staff { StaffNumber = $"S1-{tag}", FullName = "Subject Teacher", Email = $"s1-{tag}@test", UserId = Guid.NewGuid() };
        var formTeacher = new Staff { StaffNumber = $"S2-{tag}", FullName = "Form Teacher", Email = $"s2-{tag}@test", UserId = Guid.NewGuid() };
        classA.FormTeacher = formTeacher;

        db.ClassSubjects.AddRange(
            new ClassSubject { ClassRoom = classA, Subject = maths, Teacher = subjectTeacher },
            new ClassSubject { ClassRoom = classA, Subject = english },
            new ClassSubject { ClassRoom = classB, Subject = maths },
            new ClassSubject { ClassRoom = classB, Subject = english });

        var parent = new Guardian { FullName = "Parent", Phone = "+2340000000000", UserId = Guid.NewGuid() };
        var ownChild = new Student
        {
            AdmissionNumber = $"A-{tag}", FirstName = "Own", LastName = "Child", ClassRoom = classA, UserId = Guid.NewGuid(),
            Guardians = [new StudentGuardian { Guardian = parent, Relationship = GuardianRelationship.Mother }],
        };
        var otherChild = new Student
        {
            AdmissionNumber = $"B-{tag}", FirstName = "Other", LastName = "Child", ClassRoom = classB, UserId = Guid.NewGuid(),
            Guardians = [new StudentGuardian { Guardian = new Guardian { FullName = "Other Parent", Phone = "+2341111111111", UserId = Guid.NewGuid() } }],
        };
        db.Students.AddRange(ownChild, otherChild);
        await db.SaveChangesAsync();

        return new School(parent.UserId!.Value, ownChild.Id, ownChild.UserId!.Value, otherChild.Id,
            subjectTeacher.UserId!.Value, formTeacher.UserId!.Value, classA.Id, classB.Id, maths.Id, english.Id);
    }
}
