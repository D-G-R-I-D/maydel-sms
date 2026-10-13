using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.SaveAssessmentSettings;

/// <summary>
/// Replaces the score components (list order = report column order) and the grading scale (E2.4). Active components must
/// total 100. Components with scores are deactivated rather than deleted, and their maximum can't drop below a score
/// already entered. Changing bands doesn't regrade existing scores until they are next saved, and never touches published reports.
/// </summary>
public sealed record SaveAssessmentSettingsCommand(IReadOnlyList<ComponentInput> Components, IReadOnlyList<GradeBandDto> Bands) : ICommand<AssessmentSettings>;
