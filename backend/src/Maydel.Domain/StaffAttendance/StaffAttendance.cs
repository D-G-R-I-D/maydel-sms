using System.Security.Cryptography;
using System.Text;
using Maydel.Domain.Attendance;
using Maydel.Domain.Common;

namespace Maydel.Domain.StaffAttendance;

public enum ClockMethod
{
    /// <summary>Scanned the rotating QR code at the school.</summary>
    Qr,
    /// <summary>Entered or corrected by an admin (with a reason).</summary>
    Manual,
}

/// <summary>
/// The school's clock-in rules (staff attendance): working hours, the late threshold, the optional location check, and the
/// kiosk's secrets. One row per installation.
/// </summary>
public class ClockSettings : Entity
{
    public TimeOnly StartsAt { get; set; } = new(7, 30);
    public TimeOnly EndsAt { get; set; } = new(16, 0);

    /// <summary>Minutes after the start time before an arrival counts as late.</summary>
    public int GraceMinutes { get; set; } = 15;

    /// <summary>When on, a scan must come with the phone's location within <see cref="RadiusMeters"/> of the school.</summary>
    public bool RequireLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int RadiusMeters { get; set; } = 150;

    /// <summary>Signs the rotating codes. Never leaves the server.</summary>
    public string CodeSecret { get; set; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>SHA-256 of the kiosk link's token (the token itself is shown once to the admin).</summary>
    public string? KioskTokenHash { get; set; }

    public const int WindowSeconds = 30;

    public DomainError? Check()
    {
        if (EndsAt <= StartsAt) return new("clock.hours", "The working day must end after it starts.", "endsAt");
        if (GraceMinutes is < 0 or > 180) return new("clock.grace", "Grace must be between 0 and 180 minutes.", "graceMinutes");
        if (RadiusMeters is < 30 or > 5000) return new("clock.radius", "The radius must be between 30 m and 5 km.", "radiusMeters");
        if (RequireLocation && (Latitude is null || Longitude is null))
            return new("clock.location", "Set the school's location before requiring it.", "latitude");
        if (Latitude is < -90 or > 90 || Longitude is < -180 or > 180) return new("clock.location", "Enter a valid latitude and longitude.", "latitude");
        return null;
    }

    public static long WindowAt(DateTimeOffset now) => now.ToUnixTimeSeconds() / WindowSeconds;

    /// <summary>The code shown for a 30-second window: "window.signature".</summary>
    public string CodeFor(long window)
    {
        using var hmac = new HMACSHA256(Convert.FromBase64String(CodeSecret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"clock:{window}")))[..12];
        return $"{window}.{sig}";
    }

    /// <summary>Valid for the current window and the one before (a scan straddling the change still works): about 30–60 s.</summary>
    public bool Accepts(string? code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Split('.') is not [var w, _] || !long.TryParse(w, out var window)) return false;
        var current = WindowAt(now);
        if (window != current && window != current - 1) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(CodeFor(window)), Encoding.UTF8.GetBytes(code.Trim()));
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Distance in metres between two points (haversine).</summary>
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lon2 - lon1) / 2), 2);
        return 6_371_000 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public bool IsLate(DateTimeOffset clockIn) =>
        TimeOnly.FromDateTime(clockIn.ToOffset(RegisterRules.SchoolUtcOffset).DateTime) > StartsAt.AddMinutes(GraceMinutes);
}

/// <summary>One staff member's day: when they arrived and left (school days, Abuja time).</summary>
public class StaffDay : Entity
{
    public Guid StaffId { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset? ClockInAt { get; set; }
    public DateTimeOffset? ClockOutAt { get; set; }
    public ClockMethod InMethod { get; set; }
    public ClockMethod? OutMethod { get; set; }
    public double? InLatitude { get; set; }
    public double? InLongitude { get; set; }
    public bool IsLate { get; set; }

    /// <summary>Why an admin changed it (required for manual entries).</summary>
    public string? Note { get; set; }

    public TimeSpan? Worked => ClockInAt is { } i && ClockOutAt is { } o ? o - i : null;

    /// <summary>
    /// A scan: the first clocks in, the second clocks out (at least a minute later). After clocking out, the day is complete.
    /// Returns whether this scan clocked in (true) or out (false).
    /// </summary>
    public DomainError? Scan(DateTimeOffset now, ClockSettings settings, double? lat, double? lon, out bool clockedIn)
    {
        clockedIn = false;
        if (ClockInAt is null)
        {
            (ClockInAt, InMethod, InLatitude, InLongitude, IsLate) = (now, ClockMethod.Qr, lat, lon, settings.IsLate(now));
            clockedIn = true;
            return null;
        }
        if (ClockOutAt is not null) return new("clock.done", "You've already clocked in and out today.");
        if (now - ClockInAt.Value < TimeSpan.FromMinutes(1)) return new("clock.too_soon", "You've just clocked in. Scan again when you leave.");
        (ClockOutAt, OutMethod) = (now, ClockMethod.Qr);
        return null;
    }

    public DomainError? Correct(DateTimeOffset? clockIn, DateTimeOffset? clockOut, string? note, ClockSettings settings)
    {
        if (string.IsNullOrWhiteSpace(note)) return new("clock.note", "Say why the times are being changed.", "note");
        if (clockIn is null && clockOut is not null) return new("clock.order", "A clock-out needs a clock-in.", "clockInAt");
        if (clockIn is { } i && clockOut is { } o && o <= i) return new("clock.order", "Clock-out must be after clock-in.", "clockOutAt");
        if (clockIn is { } i2 && RegisterRules.TodayAt(i2) != Date) return new("clock.day", "The clock-in must be on the same day.", "clockInAt");
        if (clockOut is { } o2 && RegisterRules.TodayAt(o2) != Date) return new("clock.day", "The clock-out must be on the same day.", "clockOutAt");
        if (clockIn != ClockInAt) (ClockInAt, InMethod, IsLate) = (clockIn, ClockMethod.Manual, clockIn is { } at && settings.IsLate(at));
        if (clockOut != ClockOutAt) (ClockOutAt, OutMethod) = (clockOut, clockOut is null ? null : ClockMethod.Manual);
        Note = note.Trim();
        return null;
    }
}
