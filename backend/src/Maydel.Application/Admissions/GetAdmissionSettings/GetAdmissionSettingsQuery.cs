using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.GetAdmissionSettings;

public sealed record GetAdmissionSettingsQuery(Guid SessionId) : IQuery<AdmissionSettingsDto>;
