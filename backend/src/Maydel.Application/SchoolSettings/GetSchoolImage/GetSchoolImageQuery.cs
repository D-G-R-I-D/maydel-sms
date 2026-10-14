using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;

namespace Maydel.Application.SchoolSettings.GetSchoolImage;

/// <summary>The uploaded logo or signature (PNG). Who may fetch which is decided by the endpoint policy.</summary>
public sealed record GetSchoolImageQuery(SchoolImage Image) : IQuery<PhotoContent>;
