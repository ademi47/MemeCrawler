using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Memes.Application;
using Memes.Infrastructure.Persistence;

namespace Memes.Infrastructure.Background;

public sealed class MemeCrawlWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MemeCrawlWorker> logger) : BackgroundService
{

   public override Task StartAsync(CancellationToken ct)
    {
        logger.LogInformation("MemeCrawlWorker StartAsync fired at {ts}", DateTimeOffset.Now);
        return base.StartAsync(ct);
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); // startup delay

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var useCase = scope.ServiceProvider.GetRequiredService<GetTopMemesLast24h>();
                var repo    = scope.ServiceProvider.GetRequiredService<IMemeRepository>();

                var snapshotAt = DateTimeOffset.UtcNow;
                var posts = await useCase.HandleAsync(100, stoppingToken); // capture extra for history
                await repo.UpsertWithSnapshotAsync(posts, snapshotAt, stoppingToken);

                logger.LogInformation("Persisted {Count} meme snapshots at {When}", posts.Count, snapshotAt);
            }
            catch (OperationCanceledException) { /* normal on shutdown */ }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background crawl failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}