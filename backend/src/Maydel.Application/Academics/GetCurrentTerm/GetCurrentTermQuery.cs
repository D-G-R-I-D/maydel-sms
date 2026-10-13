using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.GetCurrentTerm;

/// <summary>The current term. Any signed-in user may read it (portal headers show it).</summary>
public sealed record GetCurrentTermQuery : IQuery<TermDto>;
