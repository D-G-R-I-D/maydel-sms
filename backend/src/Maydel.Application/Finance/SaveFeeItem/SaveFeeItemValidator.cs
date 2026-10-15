using FluentValidation;

namespace Maydel.Application.Finance.SaveFeeItem;

internal sealed class SaveFeeItemValidator : AbstractValidator<SaveFeeItemCommand>
{
    public SaveFeeItemValidator()
    {
        RuleFor(x => x.Item).NotNull();
        RuleFor(x => (x.Item.Name ?? "").Trim()).NotEmpty().MaximumLength(80).OverridePropertyName("name").When(x => x.Item is not null);
    }
}
