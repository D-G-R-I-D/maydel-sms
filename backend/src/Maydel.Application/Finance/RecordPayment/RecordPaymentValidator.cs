using FluentValidation;
using Maydel.Domain.Finance;

namespace Maydel.Application.Finance.RecordPayment;

internal sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.Method).Must(m => m is PaymentMethod.Cash or PaymentMethod.BankTransfer or PaymentMethod.Pos)
            .WithMessage("Choose cash, bank transfer or POS. Online payments are recorded automatically.");
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}
