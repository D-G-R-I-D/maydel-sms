using System.Security.Cryptography;

namespace Maydel.Application.Identity;

/// <summary>
/// Server-generated one-time passwords for new or reset accounts (E1.4, E3.3, E4.1). Shown once on the credential
/// slip and never stored or logged in plain text; the user must change it at first sign-in.
/// </summary>
public static class TemporaryPassword
{
    // No look-alikes (0/O, 1/l/I) because these get read off paper and typed by parents.
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const int Length = 12;

    /// <summary>12 characters with at least one upper-case letter, lower-case letter and digit (meets the password policy).</summary>
    public static string Generate()
    {
        const string all = Upper + Lower + Digits;
        var chars = new char[Length];
        chars[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        for (var i = 3; i < Length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}

/// <summary>What the credential slip shows. Returned exactly once, by the call that created or reset the login.</summary>
public sealed record IssuedCredentials(string UserName, string TemporaryPassword);
