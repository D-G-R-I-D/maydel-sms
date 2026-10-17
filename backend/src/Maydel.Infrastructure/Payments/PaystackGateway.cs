using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Maydel.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Payments;

/// <summary>Paystack REST API (https://paystack.com/docs/api/transaction). The secret key never leaves the server.</summary>
internal sealed class PaystackGateway(HttpClient http, IOptions<PaystackOptions> options) : IPaymentGateway
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.SecretKey);

    public async Task<GatewayCheckout> StartAsync(string email, long amountKobo, string reference, string callbackUrl, IReadOnlyDictionary<string, string> metadata, CancellationToken ct)
    {
        using var request = Authorised(HttpMethod.Post, "transaction/initialize");
        request.Content = JsonContent.Create(new { email, amount = amountKobo, currency = "NGN", reference, callback_url = callbackUrl, metadata });
        using var response = await http.SendAsync(request, ct);
        var body = await ReadAsync(response, ct);
        var data = body.GetProperty("data");
        return new GatewayCheckout(data.GetProperty("authorization_url").GetString()!, data.GetProperty("access_code").GetString()!);
    }

    public async Task<GatewayTransaction?> VerifyAsync(string reference, CancellationToken ct)
    {
        using var request = Authorised(HttpMethod.Get, $"transaction/verify/{Uri.EscapeDataString(reference)}");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        var data = (await ReadAsync(response, ct)).GetProperty("data");
        DateTimeOffset? paidAt = data.TryGetProperty("paid_at", out var p) && p.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(p.GetString(), out var at) ? at : null;
        return new GatewayTransaction(data.GetProperty("reference").GetString()!, data.GetProperty("status").GetString()!,
            data.GetProperty("amount").GetInt64(), data.GetProperty("currency").GetString()!, paidAt,
            data.TryGetProperty("gateway_response", out var g) ? g.GetString() : null);
    }

    public bool IsAuthentic(ReadOnlySpan<byte> body, string? signature)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(signature)) return false;
        var expected = HMACSHA512.HashData(Encoding.UTF8.GetBytes(options.Value.SecretKey!), body);
        byte[] given;
        try { given = Convert.FromHexString(signature); } catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    private HttpRequestMessage Authorised(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.SecretKey);
        return request;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Paystack returned {(int)response.StatusCode}: {(text.Length > 300 ? text[..300] : text)}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
