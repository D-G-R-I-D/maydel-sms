using Maydel.Application.Common.Messaging;

namespace Maydel.Application.SchoolSettings.RemoveSchoolImage;

/// <summary>Removes the uploaded logo (reports fall back to the built-in crest) or the principal's signature.</summary>
public sealed record RemoveSchoolImageCommand(SchoolImage Image) : ICommand<SchoolProfileDto>;
