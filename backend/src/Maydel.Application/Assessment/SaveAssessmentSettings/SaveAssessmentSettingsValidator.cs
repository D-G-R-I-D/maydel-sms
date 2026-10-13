using FluentValidation;

namespace Maydel.Application.Assessment.SaveAssessmentSettings;

internal sealed class SaveAssessmentSettingsValidator : AbstractValidator<SaveAssessmentSettingsCommand>
{
    public SaveAssessmentSettingsValidator()
    {
        RuleFor(x => x.Components).NotNull().Must(c => c is null || c.Count <= 12).WithMessage("Use at most 12 components.");
        RuleForEach(x => x.Components).ChildRules(c =>
        {
            c.RuleFor(x => (x.Name ?? "").Trim()).NotEmpty().MaximumLength(40).OverridePropertyName("name");
        }).OverridePropertyName("components");
        RuleFor(x => x.Bands).NotNull().Must(b => b is null || b.Count <= 15).WithMessage("Use at most 15 grades.");
        RuleForEach(x => x.Bands).ChildRules(b =>
        {
            b.RuleFor(x => (x.Grade ?? "").Trim()).NotEmpty().MaximumLength(4).OverridePropertyName("grade");
            b.RuleFor(x => (x.Remark ?? "").Trim()).NotEmpty().MaximumLength(40).OverridePropertyName("remark");
        }).OverridePropertyName("bands");
    }
}
