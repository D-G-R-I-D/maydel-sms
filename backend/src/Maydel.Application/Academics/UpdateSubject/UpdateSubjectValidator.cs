using FluentValidation;

namespace Maydel.Application.Academics.UpdateSubject;

internal sealed class UpdateSubjectValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectValidator()
    {
        RuleFor(x => (x.Name ?? "").Trim()).NotEmpty().MaximumLength(100).OverridePropertyName("name");
        // Checked as stored: the handler trims and upper-cases.
        RuleFor(x => (x.Code ?? "").Trim()).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9]+$").WithMessage("Use letters and digits only, e.g. MTH.")
            .OverridePropertyName("code");
    }
}
