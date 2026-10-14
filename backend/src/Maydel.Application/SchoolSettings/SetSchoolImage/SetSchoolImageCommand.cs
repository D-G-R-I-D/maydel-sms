using Maydel.Application.Common.Messaging;

namespace Maydel.Application.SchoolSettings.SetSchoolImage;

/// <summary>Uploads the school logo or the principal's signature (E2.6). Re-encoded as a PNG (transparency kept), at most 600 px.</summary>
public sealed record SetSchoolImageCommand(SchoolImage Image, byte[] Content) : ICommand<SchoolProfileDto>;
