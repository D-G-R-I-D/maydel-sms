using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;

namespace Maydel.Application.Academics.AddTerm;

/// <summary>Adds the First, Second or Third term to a session: one of each, in order, inside the session dates, not overlapping.</summary>
public sealed record AddTermCommand(
    Guid SessionId, TermNumber Number, DateOnly StartsOn, DateOnly EndsOn, DateOnly? MidTermStartsOn, DateOnly? MidTermEndsOn)
    : ICommand<TermDto>;
