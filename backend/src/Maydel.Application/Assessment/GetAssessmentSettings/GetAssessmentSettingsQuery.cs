using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.GetAssessmentSettings;

/// <summary>Score components (in report order) and the grading scale (highest grade first).</summary>
public sealed record GetAssessmentSettingsQuery : IQuery<AssessmentSettings>;
