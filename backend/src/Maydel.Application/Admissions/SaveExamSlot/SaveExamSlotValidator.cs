using FluentValidation;

namespace Maydel.Application.Admissions.SaveExamSlot;

internal sealed class SaveExamSlotValidator : AbstractValidator<SaveExamSlotCommand>
{
    public SaveExamSlotValidator()
    {
        RuleFor(x => x.Venue).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Mode).IsInEnum();
    }
}
