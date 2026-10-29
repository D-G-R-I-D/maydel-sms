using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Promotions.GetPromotionPlan;

/// <summary>
/// The promotion preview for a session (default: the current one): one class, or every class with students. Nothing changes.
/// </summary>
public sealed record GetPromotionPlanQuery(Guid? SessionId, Guid? ClassRoomId) : IQuery<PromotionPlan>;
