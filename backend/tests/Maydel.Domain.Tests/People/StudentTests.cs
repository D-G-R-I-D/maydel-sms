using Maydel.Domain.People;

namespace Maydel.Domain.Tests.People;

public sealed class StudentTests
{
    private static Student Student(StudentStatus status = StudentStatus.Active) =>
        new() { AdmissionNumber = "MSS/2026/0001", FirstName = "Ada", LastName = "Eze", Status = status };

    [Fact]
    public void Admission_numbers_are_zero_padded_per_year() => Assert.Equal("MSS/2026/0007", Domain.People.Student.AdmissionNumberFor(2026, 7));

    [Theory]
    [InlineData(StudentStatus.Active, StudentStatus.Suspended, true)]
    [InlineData(StudentStatus.Active, StudentStatus.Withdrawn, true)]
    [InlineData(StudentStatus.Active, StudentStatus.Graduated, true)]
    [InlineData(StudentStatus.Suspended, StudentStatus.Active, true)]
    [InlineData(StudentStatus.Suspended, StudentStatus.Withdrawn, true)]
    [InlineData(StudentStatus.Withdrawn, StudentStatus.Active, true)]
    [InlineData(StudentStatus.Withdrawn, StudentStatus.Suspended, false)]
    [InlineData(StudentStatus.Graduated, StudentStatus.Active, false)]
    [InlineData(StudentStatus.Graduated, StudentStatus.Withdrawn, false)]
    public void Status_transitions(StudentStatus from, StudentStatus to, bool allowed)
    {
        var student = Student(from);
        var error = student.ChangeStatus(to);
        Assert.Equal(allowed, error is null);
        Assert.Equal(allowed ? to : from, student.Status);
    }

    [Fact]
    public void Setting_the_same_status_is_an_error() => Assert.NotNull(Student().ChangeStatus(StudentStatus.Active));

    [Theory]
    [InlineData("2018-03-10", "2026-09-14", null)]
    [InlineData("2027-01-01", "2026-09-14", "dateOfBirth")]   // future
    [InlineData("2026-01-01", "2026-09-14", "dateOfBirth")]   // under 1 at admission
    [InlineData("1990-01-01", "2026-09-14", "dateOfBirth")]   // over 25
    [InlineData(null, "2028-01-01", "admittedOn")]           // admitted > 1 year ahead
    public void Date_rules(string? dob, string admitted, string? field)
    {
        var today = new DateOnly(2026, 10, 13);
        DateOnly? birth = dob is null ? null : DateOnly.Parse(dob);
        Assert.Equal(field, Domain.People.Student.ValidateDates(birth, DateOnly.Parse(admitted), today)?.Field);
    }
}

public sealed class GuardianPhoneTests
{
    [Theory]
    [InlineData("0803 000 0000", "+2348030000000")]
    [InlineData("08030000000", "+2348030000000")]
    [InlineData("+234 803 000 0000", "+2348030000000")]
    [InlineData("2348030000000", "+2348030000000")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    [InlineData("(0803) 000-0000", "+2348030000000")]
    public void Normalises_to_international_form(string input, string expected) => Assert.Equal(expected, Guardian.NormalizePhone(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("803 000")]
    [InlineData("call me")]
    [InlineData("0803-000-0000 ext 2")]
    public void Rejects_what_cannot_be_a_phone_number(string? input) => Assert.Null(Guardian.NormalizePhone(input));
}
