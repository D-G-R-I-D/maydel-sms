using FluentValidation;

namespace Maydel.Application.Admissions.RegisterWithGoogle;

internal sealed class RegisterWithGoogleValidator : AbstractValidator<RegisterWithGoogleCommand>
{
    public RegisterWithGoogleValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty();
        RuleFor(x => x.ParentName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.ChildFirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.ChildLastName).NotEmpty().MaximumLength(60);
    }
}
