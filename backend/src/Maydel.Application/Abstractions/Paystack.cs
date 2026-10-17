namespace Maydel.Application.Abstractions;

public sealed record GatewayCheckout(string AuthorizationUrl, string AccessCode);

/// <summary>What Paystack says about a transaction. <c>Status</c>: "success", "failed", "abandoned", "ongoing"…</summary>
public sealed record GatewayTransaction(string Reference, string Status, long AmountKobo, string Currency, DateTimeOffset? PaidAt, string? Message);

/// <summary>
/// Paystack (E10.5). Amounts are in kobo. Nothing is trusted from the browser or the webhook body alone: every payment is
/// re-verified server-side with <see cref="VerifyAsync"/> before it's recorded.
/// </summary>
public interface IPaymentGateway
{
    bool IsConfigured { get; }
    Task<GatewayCheckout> StartAsync(string email, long amountKobo, string reference, string callbackUrl, IReadOnlyDictionary<string, string> metadata, CancellationToken ct);

    /// <summary>Null when Paystack doesn't know the reference.</summary>
    Task<GatewayTransaction?> VerifyAsync(string reference, CancellationToken ct);

    /// <summary>The webhook's <c>x-paystack-signature</c>: HMAC-SHA512 of the raw body with the secret key.</summary>
    bool IsAuthentic(ReadOnlySpan<byte> body, string? signature);
}

/// <summary><c>Paystack</c> configuration. The secret key comes from the environment (<c>Paystack__SecretKey</c>), never appsettings.</summary>
public sealed class PaystackOptions
{
    public const string Section = "Paystack";
    public string? SecretKey { get; set; }
    public string? PublicKey { get; set; }

    /// <summary>Where Paystack sends the parent back to (the portal page that confirms the payment).</summary>
    public string CallbackUrl { get; set; } = "http://localhost:3000/portal/fees/paid";

    /// <summary>Where Paystack returns a family after paying for an admission form.</summary>
    public string AdmissionsCallbackUrl { get; set; } = "http://localhost:3000/admissions/paid";
}
