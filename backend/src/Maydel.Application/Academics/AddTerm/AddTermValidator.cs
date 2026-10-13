using FluentValidation;

namespace Maydel.Application.Academics.AddTerm;

internal sealed class AddTermValidator : AbstractValidator<AddTermCommand>
{
    public AddTermValidator()
    {
        RuleFor(x => x.Number).IsInEnum();
    }
}
