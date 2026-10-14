using FluentValidation;

namespace Maydel.Application.Reports.SetBehaviour;

internal sealed class SetBehaviourValidator : AbstractValidator<SetBehaviourCommand>
{
    public SetBehaviourValidator()
    {
        RuleFor(x => x.Ratings).Must(r => r.Count <= 50).WithMessage("Too many ratings.");
        RuleForEach(x => x.Ratings).ChildRules(r => r.RuleFor(x => x.Rating).MaximumLength(2)).OverridePropertyName("ratings");
        RuleFor(x => x.Clubs).Must(c => c.Count <= 20).WithMessage("Too many clubs.");
        RuleForEach(x => x.Clubs).ChildRules(c =>
        {
            c.RuleFor(x => x.Name).MaximumLength(40);
            c.RuleFor(x => x.Rating).MaximumLength(30);
        }).OverridePropertyName("clubs");
    }
}
