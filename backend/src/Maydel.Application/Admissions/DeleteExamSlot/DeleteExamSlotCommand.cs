using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.DeleteExamSlot;

/// <summary>Removes an exam sitting nobody is booked into (admin).</summary>
public sealed record DeleteExamSlotCommand(Guid Id) : ICommand;
