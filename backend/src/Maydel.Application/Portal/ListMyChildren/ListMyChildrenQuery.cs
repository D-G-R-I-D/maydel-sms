using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Portal.ListMyChildren;

/// <summary>A parent's active children, or the signed-in student themselves, each with their latest published result.</summary>
public sealed record ListMyChildrenQuery : IQuery<IReadOnlyList<PortalChild>>;
