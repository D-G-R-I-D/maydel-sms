using FluentValidation;

namespace Maydel.Application.Assessment.SaveGradeSheet;

internal sealed class SaveGradeSheetValidator : AbstractValidator<SaveGradeSheetCommand>
{
    public SaveGradeSheetValidator()
    {
        RuleFor(x => x.Entries).Cascade(CascadeMode.Stop).NotNull()
            .Must(e => e.Count <= 200).WithMessage("Save at most 200 students at a time.")
            .Must(e => e.Select(x => x.StudentId).Distinct().Count() == e.Count).WithMessage("Each student can only appear once.");
        RuleForEach(x => x.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.Scores).NotNull();
            e.RuleFor(x => x.Comment).MaximumLength(1000);
        });
    }
}
