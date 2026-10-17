using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.NotifyStageOnePasses;

/// <summary>
/// Emails everyone currently on the passed list who hasn't been told yet: "you passed the first stage; the school will contact
/// you about the next step" (admin, E11.7). Failed applicants hear nothing yet: the regret email comes with offers (E11.8).
/// </summary>
public sealed record NotifyStageOnePassesCommand(Guid SessionId) : ICommand<NotifyResult>;

public sealed record NotifyResult(int Queued);
