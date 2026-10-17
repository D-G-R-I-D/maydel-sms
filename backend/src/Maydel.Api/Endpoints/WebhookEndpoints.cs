using System.Text.Json;
using Maydel.Application.Abstractions;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Maydel.Application.Finance.ConfirmOnlinePayment;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Paystack webhook (E10.5). Anonymous but authenticated by signature: HMAC-SHA512 of the raw body with our secret key.
/// The body is only used to learn the reference; the payment itself is re-verified with Paystack's API before recording.
/// Always 200 for authentic events (Paystack retries otherwise); 401 for anything unsigned or forged.
/// </summary>
public static class WebhookEndpoints
{
    public const int MaxBodyBytes = 64 * 1024;

    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var hooks = app.MapGroup("/api/webhooks").WithTags("Webhooks").AllowAnonymous();
        hooks.MapPost("/paystack", Paystack).WithName("PaystackWebhook").ExcludeFromDescription();
    }

    private static async Task<IResult> Paystack(HttpRequest request, IPaymentGateway gateway, ICommandHandler<ConfirmOnlinePaymentCommand, OnlinePaymentResult> confirm,
        ILoggerFactory logs, CancellationToken ct)
    {
        if (request.ContentLength is > MaxBodyBytes) return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxBodyBytes) return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var body = buffer.ToArray();
        if (!gateway.IsAuthentic(body, request.Headers["x-paystack-signature"])) return TypedResults.Unauthorized();

        string? reference = null, kind = null;
        try
        {
            using var json = JsonDocument.Parse(body);
            kind = json.RootElement.GetProperty("event").GetString();
            if (json.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("reference", out var r)) reference = r.GetString();
        }
        catch (JsonException) { return TypedResults.BadRequest(); }

        if (kind == "charge.success" && !string.IsNullOrWhiteSpace(reference))
        {
            var result = await confirm.Handle(new ConfirmOnlinePaymentCommand(reference), ct);
            if (result.IsFailure) logs.CreateLogger("Paystack").LogInformation("Webhook for unknown reference {Reference}", reference);
        }
        return TypedResults.Ok();
    }
}
