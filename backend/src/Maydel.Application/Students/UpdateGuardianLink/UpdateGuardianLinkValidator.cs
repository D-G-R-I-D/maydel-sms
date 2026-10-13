using FluentValidation;

namespace Maydel.Application.Students.UpdateGuardianLink;

internal sealed class UpdateGuardianLinkValidator : AbstractValidator<UpdateGuardianLinkCommand>
{
    public UpdateGuardianLinkValidator() => RuleFor(x => x.Relationship).IsInEnum();
}
