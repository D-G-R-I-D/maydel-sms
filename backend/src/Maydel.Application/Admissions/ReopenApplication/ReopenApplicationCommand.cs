using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.ReopenApplication;

/// <summary>Gives a Missed application a fresh exam window from today (admin) and emails the family the new deadline.</summary>
public sealed record ReopenApplicationCommand(Guid ApplicationId) : ICommand<ApplicationDetail>;
