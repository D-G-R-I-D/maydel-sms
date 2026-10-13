using FluentValidation;

namespace Maydel.Application.Reports.SaveAttendance;

internal sealed class SaveAttendanceValidator : AbstractValidator<SaveAttendanceCommand>
{
    public SaveAttendanceValidator()
    {
        RuleFor(x => x.DaysOpened).InclusiveBetween(1, 200);
        RuleFor(x => x.Entries).Cascade(CascadeMode.Stop).NotNull()
            .Must(e => e.Select(x => x.StudentId).Distinct().Count() == e.Count).WithMessage("Each student can only appear once.");
    }
}
