using FluentValidation;

namespace Maydel.Application.Admissions.SaveAdmissionSettings;

internal sealed class SaveAdmissionSettingsValidator : AbstractValidator<SaveAdmissionSettingsCommand>
{
    public SaveAdmissionSettingsValidator() => RuleFor(x => x.Instructions).MaximumLength(2000);
}
