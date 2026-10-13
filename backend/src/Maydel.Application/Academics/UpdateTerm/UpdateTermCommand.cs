using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.UpdateTerm;

/// <summary>Changes a term's dates and mid-term break. The term number can't change.</summary>
public sealed record UpdateTermCommand(Guid Id, DateOnly StartsOn, DateOnly EndsOn, DateOnly? MidTermStartsOn, DateOnly? MidTermEndsOn)
    : ICommand<TermDto>;
