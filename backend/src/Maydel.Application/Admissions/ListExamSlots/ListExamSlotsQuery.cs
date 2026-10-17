using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.ListExamSlots;

/// <summary>A session's exam sittings with seats booked, soonest first (staff).</summary>
public sealed record ListExamSlotsQuery(Guid SessionId) : IQuery<IReadOnlyList<ExamSlotDto>>;
