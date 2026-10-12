using FluentValidation;

namespace Maydel.Application.Identity.Logout;

internal sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}
