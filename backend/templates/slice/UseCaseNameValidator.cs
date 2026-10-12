using FluentValidation;

namespace Maydel.Application.ModuleName.UseCaseName;

#if (isQuery)
internal sealed class UseCaseNameValidator : AbstractValidator<UseCaseNameQuery>
#else
internal sealed class UseCaseNameValidator : AbstractValidator<UseCaseNameCommand>
#endif
{
    public UseCaseNameValidator()
    {
        // Request shape only (lengths, ranges, formats). Invariants belong in the domain entity.
    }
}
