using FluentValidation;

namespace Maydel.Application.Reports.SetPrincipalComment;

internal sealed class SetPrincipalCommentValidator : AbstractValidator<SetPrincipalCommentCommand>
{
    public SetPrincipalCommentValidator() => RuleFor(x => x.Comment).MaximumLength(2000);
}
