using Maydel.Domain.Common;

namespace Maydel.Domain.Settings;

/// <summary>
/// The school's own details (E2.6): printed on report cards and, later, receipts. One row per installation.
/// Logo and signature are private files referenced by storage key, re-encoded as PNG so transparency survives.
/// </summary>
public class SchoolProfile : Entity
{
    public required string Name { get; set; }
    public string? Motto { get; set; }
    public required string Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    /// <summary>Printed under the principal's comment.</summary>
    public string? PrincipalName { get; set; }
    public string? LogoPath { get; set; }
    public string? SignaturePath { get; set; }
}
