using FluentValidation;

namespace Maydel.Application.Identity.ChangePassword;

// Mirrors the Identity password policy (docs/04-security.md) so the UI gets per-field messages.
internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Use at least 10 characters.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Include an upper-case letter.")
            .Matches("[a-z]").WithMessage("Include a lower-case letter.")
            .Matches("[0-9]").WithMessage("Include a digit.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Choose a password different from your current one.");
    }
}
