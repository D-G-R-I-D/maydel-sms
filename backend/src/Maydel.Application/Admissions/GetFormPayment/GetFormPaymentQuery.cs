using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.GetFormPayment;

/// <summary>The family's return page after paying for a form: confirms (if the webhook hasn't) and reports the outcome.</summary>
public sealed record GetFormPaymentQuery(string Reference) : IQuery<FormPayments.Outcome>;
