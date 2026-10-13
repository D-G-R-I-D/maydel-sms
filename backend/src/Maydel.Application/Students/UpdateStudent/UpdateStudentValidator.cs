using FluentValidation;

namespace Maydel.Application.Students.UpdateStudent;

internal sealed class UpdateStudentValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentValidator()
    {
        RuleFor(x => (x.FirstName ?? "").Trim()).NotEmpty().MaximumLength(100).OverridePropertyName("firstName");
        RuleFor(x => (x.LastName ?? "").Trim()).NotEmpty().MaximumLength(100).OverridePropertyName("lastName");
        RuleFor(x => x.MiddleName).MaximumLength(100);
        RuleFor(x => x.Sex).IsInEnum();
        RuleFor(x => x.StateOfOrigin).MaximumLength(100);
        RuleFor(x => x.Lga).MaximumLength(100);
        RuleFor(x => x.HomeAddress).MaximumLength(1000);
        RuleFor(x => x.House).MaximumLength(50);
        RuleFor(x => x.MedicalNotes).MaximumLength(2000);
        RuleFor(x => x.AdmittedOn).NotEmpty();
    }
}
