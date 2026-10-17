using FluentValidation;

namespace Maydel.Application.Portal.SubmitPaymentProof;

internal sealed class SubmitPaymentProofValidator : AbstractValidator<SubmitPaymentProofCommand>
{
    public SubmitPaymentProofValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).Must(a => decimal.Round(a, 2) == a).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.BankReference).MaximumLength(100);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
