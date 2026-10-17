using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;

namespace Maydel.Application.Portal.ListChildProofs;

/// <summary>The proofs of payment sent for a child, newest first, with their review status.</summary>
public sealed record ListChildProofsQuery(Guid StudentId) : IQuery<IReadOnlyList<ProofDto>>;
