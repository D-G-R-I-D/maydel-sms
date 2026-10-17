using FluentValidation;

namespace Maydel.Application.Admissions.SaveApplicationForm;

internal sealed class SaveApplicationFormValidator : AbstractValidator<SaveApplicationFormCommand>
{
    public SaveApplicationFormValidator()
    {
        RuleFor(x => x.Body.ChildFirstName).MaximumLength(60).OverridePropertyName("childFirstName");
        RuleFor(x => x.Body.ChildLastName).MaximumLength(60).OverridePropertyName("childLastName");
        RuleFor(x => x.Body.Form.HomeAddress).MaximumLength(300).OverridePropertyName("homeAddress");
        RuleFor(x => x.Body.Form.MedicalNotes).MaximumLength(1000).OverridePropertyName("medicalNotes");
        RuleFor(x => x.Body.Form.SpecialNeeds).MaximumLength(1000).OverridePropertyName("specialNeeds");
        RuleFor(x => x.Body.Form.FatherEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Body.Form.FatherEmail)).OverridePropertyName("fatherEmail");
        RuleFor(x => x.Body.Form.MotherEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Body.Form.MotherEmail)).OverridePropertyName("motherEmail");
        foreach (var (get, name) in new (Func<SaveApplicationFormCommand, string?>, string)[]
        {
            (x => x.Body.Form.ChildMiddleName, "childMiddleName"), (x => x.Body.Form.StateOfOrigin, "stateOfOrigin"), (x => x.Body.Form.Lga, "lga"),
            (x => x.Body.Form.Religion, "religion"), (x => x.Body.Form.PreviousSchool, "previousSchool"), (x => x.Body.Form.PreviousClass, "previousClass"),
            (x => x.Body.Form.FatherName, "fatherName"), (x => x.Body.Form.FatherOccupation, "fatherOccupation"), (x => x.Body.Form.MotherName, "motherName"),
            (x => x.Body.Form.MotherOccupation, "motherOccupation"), (x => x.Body.Form.EmergencyContactName, "emergencyContactName"),
        })
            RuleFor(x => get(x)).MaximumLength(120).OverridePropertyName(name);
        foreach (var (get, name) in new (Func<SaveApplicationFormCommand, string?>, string)[]
        {
            (x => x.Body.Form.FatherPhone, "fatherPhone"), (x => x.Body.Form.MotherPhone, "motherPhone"), (x => x.Body.Form.EmergencyContactPhone, "emergencyContactPhone"),
        })
            RuleFor(x => get(x)).MaximumLength(30).OverridePropertyName(name);
    }
}
