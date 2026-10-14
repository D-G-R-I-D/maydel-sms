using Maydel.Application.Common.Messaging;

namespace Maydel.Application.SchoolSettings.UpdateSchoolProfile;

/// <summary>Replaces the school's details (E2.6). Blank optional fields are cleared. Images have their own endpoints.</summary>
public sealed record UpdateSchoolProfileCommand(
    string Name, string? Motto, string Address, string? Phone, string? Email, string? Website, string? PrincipalName) : ICommand<SchoolProfileDto>;
