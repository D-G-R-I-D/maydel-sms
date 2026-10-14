using FluentValidation;

namespace Maydel.Application.SchoolSettings.UpdateSchoolProfile;

internal sealed class UpdateSchoolProfileValidator : AbstractValidator<UpdateSchoolProfileCommand>
{
    public UpdateSchoolProfileValidator()
    {
        RuleFor(x => (x.Name ?? "").Trim()).NotEmpty().MaximumLength(120).OverridePropertyName("name");
        RuleFor(x => (x.Address ?? "").Trim()).NotEmpty().MaximumLength(300).OverridePropertyName("address");
        RuleFor(x => x.Motto).MaximumLength(120);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Email).MaximumLength(120).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Website).MaximumLength(120);
        RuleFor(x => x.PrincipalName).MaximumLength(80);
    }
}
