using FluentValidation;

namespace Maydel.Application.Academics.UpdateSession;

internal sealed class UpdateSessionValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(9);
    }
}
