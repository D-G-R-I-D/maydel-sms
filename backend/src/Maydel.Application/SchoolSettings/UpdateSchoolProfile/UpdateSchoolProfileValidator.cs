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
        RuleFor(x => x.WhatsApp).MaximumLength(40)
            .Must(v => string.IsNullOrWhiteSpace(v) || Domain.People.Guardian.NormalizePhone(v) is not null).WithMessage("Enter a valid WhatsApp number, e.g. 0803 123 4567.");
        RuleFor(x => x.OfficeHours).MaximumLength(120);
        foreach (var (get, name, max) in new (Func<UpdateSchoolProfileCommand, string?>, string, int)[]
            { (x => x.MapUrl, "mapUrl", 500), (x => x.FacebookUrl, "facebookUrl", 300), (x => x.InstagramUrl, "instagramUrl", 300) })
            RuleFor(x => get(x)).MaximumLength(max)
                .Must(v => string.IsNullOrWhiteSpace(v) || (Uri.TryCreate(v.Trim(), UriKind.Absolute, out var u) && u.Scheme == "https"))
                .WithMessage("Enter a full https:// link.").OverridePropertyName(name);
    }
}
