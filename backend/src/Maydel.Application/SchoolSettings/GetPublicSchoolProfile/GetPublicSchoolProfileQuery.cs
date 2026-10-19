using Maydel.Application.Common.Messaging;

namespace Maydel.Application.SchoolSettings.GetPublicSchoolProfile;

/// <summary>The school's public details for the website header, footer and contact page (E7.1).</summary>
public sealed record GetPublicSchoolProfileQuery : IQuery<PublicSchoolProfile>;
