using FluentValidation;

namespace Maydel.Application.Attendance.SaveRegister;

internal sealed class SaveRegisterValidator : AbstractValidator<SaveRegisterCommand>
{
    public SaveRegisterValidator()
    {
        RuleFor(x => x.Entries).NotNull().Must(e => e is null || e.Count <= 200).WithMessage("Too many entries.");
        RuleForEach(x => x.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.Status).IsInEnum();
            e.RuleFor(x => x.Note).MaximumLength(200);
        }).OverridePropertyName("entries");
        RuleFor(x => x.Entries).Must(e => e is null || e.Select(x => x.StudentId).Distinct().Count() == e.Count)
            .WithMessage("Each student can only appear once.");
    }
}
