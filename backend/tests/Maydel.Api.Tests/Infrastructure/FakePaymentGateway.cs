using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Maydel.Application.Abstractions;

namespace Maydel.Api.Tests.Infrastructure;

/// <summary>Paystack stand-in: tests decide what "verify" reports per reference; signatures use a fixed test secret.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string Secret = "sk_test_fake_secret";
    public ConcurrentDictionary<string, GatewayTransaction> Transactions { get; } = new();
    public ConcurrentDictionary<string, long> Started { get; } = new();

    public bool IsConfigured => true;

    public Task<GatewayCheckout> StartAsync(string email, long amountKobo, string reference, string callbackUrl, IReadOnlyDictionary<string, string> metadata, CancellationToken ct)
    {
        Started[reference] = amountKobo;
        return Task.FromResult(new GatewayCheckout($"https://checkout.paystack.test/{reference}", "access-" + reference));
    }

    public Task<GatewayTransaction?> VerifyAsync(string reference, CancellationToken ct) =>
        Task.FromResult(Transactions.TryGetValue(reference, out var tx) ? tx : null);

    public bool IsAuthentic(ReadOnlySpan<byte> body, string? signature) =>
        signature is not null && Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), body)).Equals(signature, StringComparison.OrdinalIgnoreCase);

    /// <summary>Marks the reference as paid with exactly what was asked (or <paramref name="kobo"/>).</summary>
    public void Pay(string reference, long? kobo = null, string status = "success", string currency = "NGN") =>
        Transactions[reference] = new GatewayTransaction(reference, status, kobo ?? Started[reference], currency, DateTimeOffset.UtcNow, status);

    public static string Sign(string body) => Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
}
