using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.GetAdmissionsInfo;

/// <summary>Sessions currently open for admissions, with fee, exam window and classes (public "Buy admission form" page).</summary>
public sealed record GetAdmissionsInfoQuery : IQuery<IReadOnlyList<AdmissionsInfo>>;
