using FluentValidation;

namespace Maydel.Application.Academics.CreateSession;

internal sealed class CreateSessionValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(9);
    }
}
