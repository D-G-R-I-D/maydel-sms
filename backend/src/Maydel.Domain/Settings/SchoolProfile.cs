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

    // Website contact details (E7.1): read by the public site instead of being hard-coded.
    /// <summary>WhatsApp number for the "Chat on WhatsApp" link, e.g. +2348031234567.</summary>
    public string? WhatsApp { get; set; }
    /// <summary>A Google Maps link to the school (https://maps.app.goo.gl/… or https://www.google.com/maps/…).</summary>
    public string? MapUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    /// <summary>Free text, e.g. "Mon–Fri 7:30am – 4:00pm".</summary>
    public string? OfficeHours { get; set; }
    public int? FoundedYear { get; set; }

    /// <summary>Show live numbers (students, teachers, levels) on the website (E7.1).</summary>
    public bool ShowStatsOnWebsite { get; set; } = true;
    public string? SignaturePath { get; set; }
}
