using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;

namespace Maydel.Application.Finance.ListOnlinePayments;

/// <summary>Online payment attempts, newest first; filter <c>NeedsReview</c> to find money to refund or reallocate.</summary>
public sealed record ListOnlinePaymentsQuery(OnlinePaymentStatus? Status = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<OnlinePaymentRow>>;
