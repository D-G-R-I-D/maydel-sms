using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.GetBehaviourSettings;

/// <summary>The behaviour traits rated on report cards and the rating key (E2.5).</summary>
public sealed record GetBehaviourSettingsQuery : IQuery<BehaviourSettings>;
