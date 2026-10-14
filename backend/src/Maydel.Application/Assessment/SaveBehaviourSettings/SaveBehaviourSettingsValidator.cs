using FluentValidation;

namespace Maydel.Application.Assessment.SaveBehaviourSettings;

internal sealed class SaveBehaviourSettingsValidator : AbstractValidator<SaveBehaviourSettingsCommand>
{
    public SaveBehaviourSettingsValidator()
    {
        RuleFor(x => x.Traits).NotNull().Must(t => t is null || t.Count <= 30).WithMessage("Use at most 30 traits.");
        RuleForEach(x => x.Traits).ChildRules(t =>
        {
            t.RuleFor(x => (x.Name ?? "").Trim()).NotEmpty().MaximumLength(60).OverridePropertyName("name");
        }).OverridePropertyName("traits");
        RuleFor(x => x.Traits)
            .Must(t => t is null || t.Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).All(g => g.Count() == 1))
            .WithMessage("Trait names must be different.");
    }
}
