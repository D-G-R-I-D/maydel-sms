using Maydel.Domain.Admissions;

namespace Maydel.Domain.Tests.Admissions;

public sealed class AdmissionTests
{
    private static AdmissionApplication Application() => new()
    {
        ReferenceNumber = "APP-2026-0001", ParentName = "Mrs. Eze", Phone = "+2348030000000", Email = "a@example.com",
        ChildFirstName = "Kamsi", ChildLastName = "Eze",
    };

    [Fact]
    public void Paying_for_the_form_unlocks_it_and_opens_the_exam_window_once()
    {
        var application = Application();
        Assert.Null(application.MarkFormPaid(FormPaymentMethod.SchoolCode, "ABCD-EFGH", DateTimeOffset.UtcNow, new DateOnly(2026, 10, 15), 5));
        Assert.Equal((ApplicationStatus.FormPaid, (DateOnly?)new DateOnly(2026, 10, 20)), (application.Status, application.ExamWindowEndsOn));
        Assert.Equal("admission.already_paid", application.MarkFormPaid(FormPaymentMethod.Online, null, DateTimeOffset.UtcNow, new DateOnly(2026, 10, 15), 5)?.Code);
    }

    [Theory]
    [InlineData("abcd efgh", "ABCD-EFGH")]
    [InlineData("ABCD-EFGH", "ABCD-EFGH")]
    [InlineData("ABCD-EFG0", null)]   // 0 isn't in the alphabet
    [InlineData("ABC", null)]
    public void Form_codes_are_normalised_and_unambiguous(string input, string? expected) =>
        Assert.Equal(expected, FormCode.Normalise(input));

    [Fact]
    public void Generated_codes_use_only_the_alphabet()
    {
        var code = FormCode.Generate(Random.Shared.Next);
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", code);
        Assert.Equal(code, FormCode.Normalise(code));
    }

    [Theory]
    [InlineData(5000, 5, null)]
    [InlineData(-1, 5, "admission.fee")]
    [InlineData(5000, 0, "admission.window")]
    [InlineData(5000, 31, "admission.window")]
    public void Settings_are_checked(decimal fee, int days, string? code) => Assert.Equal(code, AdmissionSettings.Check(fee, days)?.Code);
}

public sealed class ApplicationFormTests
{
    private static AdmissionApplication Paid()
    {
        var a = new AdmissionApplication
        {
            ReferenceNumber = "APP-2026-0002", ParentName = "Mr. Obi", Phone = "080", Email = "o@example.com", ChildFirstName = "Ike", ChildLastName = "Obi",
        };
        a.MarkFormPaid(FormPaymentMethod.Online, null, DateTimeOffset.UtcNow, new DateOnly(2026, 10, 15), 5);
        return a;
    }

    private static ApplicationForm Complete() => new()
    {
        ChildSex = Maydel.Domain.People.Sex.Male, HomeAddress = "1 Crescent, Abuja", FatherName = "Mr. Obi", FatherPhone = "0803", Declaration = true,
    };

    [Fact]
    public void The_form_is_editable_only_after_paying_and_before_submitting()
    {
        var unpaid = new AdmissionApplication { ReferenceNumber = "x", ParentName = "p", Phone = "1", Email = "e", ChildFirstName = "a", ChildLastName = "b" };
        Assert.Equal("admission.form_locked", unpaid.SaveForm(Complete(), "a", "b", null)?.Code);

        var app = Paid();
        Assert.Null(app.SaveForm(new ApplicationForm { HomeAddress = "  draft  " }, " Ike ", "Obi", null));
        Assert.Equal(("draft", "Ike"), (app.Form.HomeAddress, app.ChildFirstName));
    }

    [Fact]
    public void Submitting_needs_the_essentials_a_parent_and_a_passport_photo()
    {
        var app = Paid();
        app.SaveForm(Complete(), "Ike", "Obi", null);
        Assert.Equal("childDateOfBirth", app.Submit(DateTimeOffset.UtcNow)?.Field);

        app.SaveForm(Complete(), "Ike", "Obi", new DateOnly(2020, 1, 1));
        Assert.Equal("documents", app.Submit(DateTimeOffset.UtcNow)?.Field);

        app.Documents.Add(new ApplicationDocument { Kind = DocumentKind.PassportPhoto, FilePath = "a", ContentType = "image/jpeg" });
        Assert.Null(app.Submit(DateTimeOffset.UtcNow));
        Assert.Equal(ApplicationStatus.Submitted, app.Status);
        Assert.Equal("admission.form_locked", app.SaveForm(Complete(), "Ike", "Obi", null)?.Code);
    }

    [Theory]
    [InlineData(false, true, true, "fatherName")]
    [InlineData(true, false, true, "homeAddress")]
    [InlineData(true, true, false, "declaration")]
    public void Missing_pieces_are_reported_by_field(bool parent, bool address, bool declaration, string field)
    {
        var form = new ApplicationForm
        {
            ChildSex = Maydel.Domain.People.Sex.Female, HomeAddress = address ? "x" : null, MotherName = parent ? "Mrs. A" : null,
            MotherPhone = parent ? "0802" : null, Declaration = declaration,
        };
        Assert.Equal(field, form.Missing()?.Field);
    }
}

public sealed class CorrectFormTests
{
    private static AdmissionApplication Submitted()
    {
        var a = new AdmissionApplication { ReferenceNumber = "APP-2026-0003", ParentName = "p", Phone = "1", Email = "e", ChildFirstName = "Ada", ChildLastName = "Eze" };
        a.MarkFormPaid(FormPaymentMethod.Online, null, DateTimeOffset.UtcNow, new DateOnly(2026, 10, 15), 5);
        a.SaveForm(Complete(), "Ada", "Eze", new DateOnly(2020, 2, 2));
        a.Documents.Add(new ApplicationDocument { Kind = DocumentKind.PassportPhoto, FilePath = "a", ContentType = "image/jpeg" });
        Assert.Null(a.Submit(DateTimeOffset.UtcNow));
        return a;
    }

    private static ApplicationForm Complete() => new()
    {
        ChildSex = Maydel.Domain.People.Sex.Female, HomeAddress = "2 Close, Abuja", MotherName = "Mrs. Eze", MotherPhone = "0802", Declaration = true,
    };

    [Fact]
    public void An_admin_can_correct_a_submitted_form_but_it_must_stay_complete()
    {
        var app = Submitted();
        var fixedForm = Complete();
        fixedForm.HomeAddress = "5 New Road, Abuja";
        Assert.Null(app.CorrectForm(fixedForm, "Adaeze", "Eze", new DateOnly(2020, 2, 3)));
        Assert.Equal(("5 New Road, Abuja", "Adaeze", ApplicationStatus.Submitted), (app.Form.HomeAddress, app.ChildFirstName, app.Status));

        Assert.Equal("homeAddress", app.CorrectForm(new ApplicationForm { ChildSex = Maydel.Domain.People.Sex.Female, Declaration = true }, "A", "E", new DateOnly(2020, 1, 1))?.Field);
        Assert.Equal("childDateOfBirth", app.CorrectForm(Complete(), "A", "E", null)?.Field);
    }

    [Fact]
    public void Drafts_and_closed_applications_cannot_be_corrected()
    {
        var draft = new AdmissionApplication { ReferenceNumber = "x", ParentName = "p", Phone = "1", Email = "e", ChildFirstName = "a", ChildLastName = "b" };
        Assert.Equal("admission.not_submitted", draft.CorrectForm(Complete(), "a", "b", new DateOnly(2020, 1, 1))?.Code);
        var enrolled = Submitted();
        enrolled.Status = ApplicationStatus.Enrolled;
        Assert.Equal("admission.closed_application", enrolled.CorrectForm(Complete(), "a", "b", new DateOnly(2020, 1, 1))?.Code);
    }
}
