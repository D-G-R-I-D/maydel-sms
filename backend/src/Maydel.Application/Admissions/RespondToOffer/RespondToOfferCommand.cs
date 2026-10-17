using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.RespondToOffer;

/// <summary>The family accepts or declines the offer (E11.8). Accepting asks the school to enrol the child.</summary>
public sealed record RespondToOfferCommand(Guid ApplicationId, bool Accept) : ICommand<ApplicationDetail>;
