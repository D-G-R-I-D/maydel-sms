using FluentValidation;

namespace Maydel.Application.Admissions.SetDecisions;

internal sealed class SetDecisionsValidator : AbstractValidator<SetDecisionsCommand>
{
    public SetDecisionsValidator()
    {
        RuleFor(x => x.ApplicationIds).NotEmpty().Must(ids => ids.Count <= 500).WithMessage("Choose up to 500 applicants at a time.");
        RuleFor(x => x.Decision).IsInEnum();
    }
}
