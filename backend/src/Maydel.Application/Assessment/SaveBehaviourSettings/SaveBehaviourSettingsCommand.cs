using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.SaveBehaviourSettings;

/// <summary>
/// Replaces the list of behaviour traits (E2.5); list order = order on the report. Traits already rated on a report are
/// deactivated rather than deleted, so published reports keep them.
/// </summary>
public sealed record SaveBehaviourSettingsCommand(IReadOnlyList<TraitInput> Traits) : ICommand<BehaviourSettings>;
