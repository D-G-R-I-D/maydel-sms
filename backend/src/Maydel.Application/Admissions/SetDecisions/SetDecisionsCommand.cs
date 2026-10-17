using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;

namespace Maydel.Application.Admissions.SetDecisions;

/// <summary>Ticks applicants for an offer (or clears the tick) as a draft; nothing is sent until the decisions are published (admin).</summary>
public sealed record SetDecisionsCommand(IReadOnlyList<Guid> ApplicationIds, OfferDecision? Decision) : ICommand<IReadOnlyList<ExamCandidate>>;
