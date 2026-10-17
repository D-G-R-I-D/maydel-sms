using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.BookExam;

/// <summary>Books or moves the family's entrance exam (E11.6). Seat counts are checked under optimistic concurrency.</summary>
public sealed record BookExamCommand(Guid ApplicationId, Guid SlotId) : ICommand<ApplicationDetail>;
