using FluentValidation;
using Maydel.Domain.People;

namespace Maydel.Application.Students.AddGuardian;

internal sealed class AddGuardianValidator : AbstractValidator<AddGuardianCommand>
{
    public AddGuardianValidator()
    {
        RuleFor(x => (x.FullName ?? "").Trim()).NotEmpty().MaximumLength(150).OverridePropertyName("fullName");
        RuleFor(x => x.Phone).NotEmpty().Must(p => Guardian.NormalizePhone(p) is not null)
            .WithMessage("Enter a phone number, e.g. 0803 000 0000 or +234 803 000 0000.");
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Occupation).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(1000);
        RuleFor(x => x.Relationship).IsInEnum();
    }
}
