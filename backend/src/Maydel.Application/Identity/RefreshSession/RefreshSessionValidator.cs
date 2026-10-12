using FluentValidation;

namespace Maydel.Application.Identity.RefreshSession;

internal sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}
