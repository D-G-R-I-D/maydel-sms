using FluentValidation;

namespace Maydel.Application.Finance.AdjustInvoice;

internal sealed class AdjustInvoiceValidator : AbstractValidator<AdjustInvoiceCommand>
{
    public AdjustInvoiceValidator() => RuleFor(x => x.Description).MaximumLength(120);
}
