using FluentValidation;

namespace Maydel.Application.Students.ChangeStudentStatus;

internal sealed class ChangeStudentStatusValidator : AbstractValidator<ChangeStudentStatusCommand>
{
    public ChangeStudentStatusValidator() => RuleFor(x => x.Status).IsInEnum();
}
