using Maydel.Application.Common.Messaging;
using Maydel.Application.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maydel.Infrastructure.Finance;

/// <summary>Releases unpaid parent shop orders once their hold runs out (checks every 15 minutes).</summary>
internal sealed class StoreWorker(IServiceScopeFactory scopes, ILogger<StoreWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var release = scope.ServiceProvider.GetRequiredService<ICommandHandler<ReleaseExpiredOrdersCommand, int>>();
                if (await release.Handle(new ReleaseExpiredOrdersCommand(), stoppingToken) is { IsSuccess: true, Value: > 0 } released)
                    logger.LogInformation("Released {Count} unpaid shop orders", released.Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Store release run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
