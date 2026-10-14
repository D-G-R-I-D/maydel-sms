using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Portal.ListChildResults;

/// <summary>The child's published term results, newest first (E6.2). Drafts are never listed.</summary>
public sealed record ListChildResultsQuery(Guid StudentId) : IQuery<IReadOnlyList<PortalResultSummary>>;
