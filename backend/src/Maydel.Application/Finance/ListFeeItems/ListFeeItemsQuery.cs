using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.ListFeeItems;

/// <summary>A term's fee items: school-wide ones first, then per class level.</summary>
public sealed record ListFeeItemsQuery(Guid TermId) : IQuery<IReadOnlyList<FeeItemDto>>;
