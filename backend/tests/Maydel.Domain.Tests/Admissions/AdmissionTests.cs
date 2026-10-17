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
