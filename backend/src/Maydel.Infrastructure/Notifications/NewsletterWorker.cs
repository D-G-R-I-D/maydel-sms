using Maydel.Application.Common.Messaging;
using Maydel.Application.Newsletters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maydel.Infrastructure.Notifications;

/// <summary>Sends scheduled newsletters when their time comes (checks every minute).</summary>
internal sealed class NewsletterWorker(IServiceScopeFactory scopes, ILogger<NewsletterWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var send = scope.ServiceProvider.GetRequiredService<ICommandHandler<SendDueNewslettersCommand, int>>();
                if (await send.Handle(new SendDueNewslettersCommand(), stoppingToken) is { IsSuccess: true, Value: > 0 } sent)
                    logger.LogInformation("Sent {Count} scheduled newsletters", sent.Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled newsletter run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
