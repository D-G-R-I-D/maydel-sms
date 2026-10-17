using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;

namespace Maydel.Application.Finance.ListPaymentProofs;

/// <summary>
/// Proofs of payment. By default the Pending queue, oldest first so nobody waits long; <c>Status</c> picks another
/// status, <c>All</c> shows every status newest first.
/// </summary>
public sealed record ListPaymentProofsQuery(ProofStatus? Status = null, bool All = false, int? Page = null, int? PageSize = null) : IQuery<PagedResult<ProofDto>>;
