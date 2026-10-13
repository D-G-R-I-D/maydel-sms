using FluentValidation;

namespace Maydel.Application.Academics.CreateSubject;

internal sealed class CreateSubjectValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectValidator()
    {
        RuleFor(x => (x.Name ?? "").Trim()).NotEmpty().MaximumLength(100).OverridePropertyName("name");
        // Checked as stored: the handler trims and upper-cases.
        RuleFor(x => (x.Code ?? "").Trim()).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9]+$").WithMessage("Use letters and digits only, e.g. MTH.")
            .OverridePropertyName("code");
    }
}
