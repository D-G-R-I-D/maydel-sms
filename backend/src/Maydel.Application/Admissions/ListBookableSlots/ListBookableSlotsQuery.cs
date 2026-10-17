using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.ListBookableSlots;

/// <summary>Exam times the family can choose: their session, in their window, not started, with seats (plus their current one).</summary>
public sealed record ListBookableSlotsQuery(Guid ApplicationId) : IQuery<IReadOnlyList<ExamSlotOption>>;
