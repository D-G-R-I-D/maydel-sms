using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.PublishDecisions;

/// <summary>
/// Publishes the session's decisions (admin, E11.8): every applicant with a score becomes Offered (ticked; admission number
/// reserved; email with the admission letter and welcome guide) or NotOffered (a kind regret email). Applicants still waiting
/// for a score aren't touched, so it can be run again later.
/// </summary>
public sealed record PublishDecisionsCommand(Guid SessionId) : ICommand<PublishDecisionsResult>;

public sealed record PublishDecisionsResult(int Offered, int NotOffered);
