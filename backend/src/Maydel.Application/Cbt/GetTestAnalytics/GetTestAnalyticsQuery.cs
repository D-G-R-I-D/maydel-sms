using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetTestAnalytics;

/// <summary>Score spread, class averages and how each question went (E16.7).</summary>
public sealed record GetTestAnalyticsQuery(Guid TestId) : IQuery<TestAnalytics>;
