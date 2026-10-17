using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;

namespace Maydel.Application.Admissions.StartFormPayment;

/// <summary>Starts a Paystack payment for the admission form fee; returns where to send the browser.</summary>
public sealed record StartFormPaymentCommand(Guid ApplicationId) : ICommand<OnlineCheckout>;
