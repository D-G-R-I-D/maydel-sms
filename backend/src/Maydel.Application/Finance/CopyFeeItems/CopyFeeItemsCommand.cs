using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.CopyFeeItems;

/// <summary>Copies a term's fee items (and who opted in to optional ones) to another term that has none yet.</summary>
public sealed record CopyFeeItemsCommand(Guid FromTermId, Guid ToTermId) : ICommand<IReadOnlyList<FeeItemDto>>;
