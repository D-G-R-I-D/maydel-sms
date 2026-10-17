using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SetCutOff;

/// <summary>Sets (or clears, with null) the session's pass mark; the results lists follow it (admin).</summary>
public sealed record SetCutOffCommand(Guid SessionId, decimal? CutOffMark) : ICommand<ExamResults>;
