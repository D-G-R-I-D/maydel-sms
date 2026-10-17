using Maydel.Application.Common.Messaging;
using Maydel.Domain.Admissions;

namespace Maydel.Application.Admissions.SaveExamSlot;

/// <summary>Creates (<c>Id</c> null) or edits an exam sitting (admin). A sitting with bookings keeps its time; seats can't drop below the bookings.</summary>
public sealed record SaveExamSlotCommand(Guid? Id, Guid SessionId, DateTimeOffset StartsAt, int DurationMinutes, string Venue, ExamMode Mode, int Capacity, string? Notes)
    : ICommand<ExamSlotDto>;
