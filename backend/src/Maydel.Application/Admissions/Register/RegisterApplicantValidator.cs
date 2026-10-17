using FluentValidation;

namespace Maydel.Application.Admissions.Register;

internal sealed class RegisterApplicantValidator : AbstractValidator<RegisterApplicantCommand>
{
    public RegisterApplicantValidator()
    {
        RuleFor(x => (x.ParentName ?? "").Trim()).NotEmpty().MaximumLength(120).OverridePropertyName("parentName");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => (x.Phone ?? "").Trim()).NotEmpty().MaximumLength(30).OverridePropertyName("phone");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).MaximumLength(128);
        RuleFor(x => (x.ChildFirstName ?? "").Trim()).NotEmpty().MaximumLength(60).OverridePropertyName("childFirstName");
        RuleFor(x => (x.ChildLastName ?? "").Trim()).NotEmpty().MaximumLength(60).OverridePropertyName("childLastName");
    }
}
