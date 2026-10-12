using FluentValidation;

namespace Maydel.Application.Identity.GoogleLogin;

internal sealed class GoogleLoginValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginValidator() => RuleFor(x => x.IdToken).NotEmpty().MaximumLength(4096);
}
