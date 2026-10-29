using Maydel.Application.Common.Messaging;
using Maydel.Application.Communication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maydel.Infrastructure.Notifications;

/// <summary>Sends the evening communication-book summary (checks hourly; the command itself sends from 18:00 WAT, once a day per person).</summary>
internal sealed class BookDigestWorker(IServiceScopeFactory scopes, ILogger<BookDigestWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var send = scope.ServiceProvider.GetRequiredService<ICommandHandler<SendBookDigestCommand, int>>();
                if (await send.Handle(new SendBookDigestCommand(), stoppingToken) is { IsSuccess: true, Value: > 0 } sent)
                    logger.LogInformation("Sent {Count} communication-book summaries", sent.Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Communication-book summary run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
