using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Promotions.UndoPromotion;

/// <summary>
/// Undoes a class's promotion for a session (default: the current one): its students go back to that class with their old status
/// (logins back on for those who were active). Students changed by hand since are left alone and listed as skipped.
/// </summary>
public sealed record UndoPromotionCommand(Guid? SessionId, Guid ClassRoomId) : ICommand<PromotionUndone>;
