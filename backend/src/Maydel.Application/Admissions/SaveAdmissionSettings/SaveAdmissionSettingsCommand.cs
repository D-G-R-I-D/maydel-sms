using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.SaveAdmissionSettings;

/// <summary>Opens or closes admissions for a session and sets the form fee and exam window (admin).</summary>
public sealed record SaveAdmissionSettingsCommand(Guid SessionId, bool IsOpen, decimal FormFee, int ExamWindowDays, string? Instructions) : ICommand<AdmissionSettingsDto>;
