using FluentValidation;

namespace Maydel.Application.Promotions.PromoteStudents;

internal sealed class PromoteStudentsValidator : AbstractValidator<PromoteStudentsCommand>
{
    public const int MaxStudents = 3000;

    public PromoteStudentsValidator()
    {
        RuleFor(x => x.Decisions).NotEmpty().WithMessage("Choose at least one student.")
            .Must(d => d.Count <= MaxStudents).WithMessage($"At most {MaxStudents} students at a time.")
            .Must(d => d.Select(x => x.StudentId).Distinct().Count() == d.Count).WithMessage("Each student can appear only once.");
        RuleForEach(x => x.Decisions).ChildRules(d => d.RuleFor(x => x.Outcome).IsInEnum());
    }
}
