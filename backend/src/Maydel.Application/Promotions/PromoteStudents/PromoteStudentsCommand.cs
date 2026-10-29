using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Promotions.PromoteStudents;

/// <summary>
/// Applies end-of-session decisions (E3.6) in one go: move up, repeat, graduate or withdraw. Opens in the session's third term.
/// All or nothing: any problem (a full class, a student already moved on this session) changes nobody. Graduating or withdrawing
/// turns the student's own login off, as a status change does.
/// </summary>
public sealed record PromoteStudentsCommand(Guid? SessionId, IReadOnlyList<PromotionDecision> Decisions) : ICommand<PromotionSummary>;
