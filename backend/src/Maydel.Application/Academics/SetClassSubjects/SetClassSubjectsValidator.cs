using FluentValidation;

namespace Maydel.Application.Academics.SetClassSubjects;

internal sealed class SetClassSubjectsValidator : AbstractValidator<SetClassSubjectsCommand>
{
    public SetClassSubjectsValidator()
    {
        RuleFor(x => x.Subjects).Cascade(CascadeMode.Stop).NotNull()
            .Must(s => s.Select(a => a.SubjectId).Distinct().Count() == s.Count).WithMessage("Each subject can only be listed once.")
            .Must(s => s.Count <= 40).WithMessage("A class can offer at most 40 subjects.");
    }
}
