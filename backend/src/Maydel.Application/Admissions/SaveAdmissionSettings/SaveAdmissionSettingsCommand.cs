using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SaveAdmissionSettings;

/// <summary>Opens or closes admissions for a session and sets the form fee and exam window (admin).</summary>
/// <remarks><c>EntryTermId</c>: the term new admits start (a term of this session); null = the first term.</remarks>
public sealed record SaveAdmissionSettingsCommand(Guid SessionId, bool IsOpen, decimal FormFee, int ExamWindowDays, string? Instructions, Guid? EntryTermId = null)
    : ICommand<AdmissionSettingsDto>;
