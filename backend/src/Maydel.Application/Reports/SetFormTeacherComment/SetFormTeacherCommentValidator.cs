using FluentValidation;

namespace Maydel.Application.Reports.SetFormTeacherComment;

internal sealed class SetFormTeacherCommentValidator : AbstractValidator<SetFormTeacherCommentCommand>
{
    public SetFormTeacherCommentValidator() => RuleFor(x => x.Comment).MaximumLength(2000);
}
