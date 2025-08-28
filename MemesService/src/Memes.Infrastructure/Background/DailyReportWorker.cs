using Memes.Infrastructure.Persistence;
using Memes.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Memes.Infrastructure.Background;

public sealed class DailyReportWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DailyReportWorker> logger) : BackgroundService
{
    private static TimeSpan TimeUntilNextUtcMidnight()
    {
        var now = DateTimeOffset.UtcNow;
        var next = now.Date.AddDays(1);
        return next - now;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // --- TEST MODE: run once after 30s, then switch to daily ---
        var initialDelay = Debugger.IsAttached ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
        logger.LogInformation("DailyReportWorker: starting, initial delay {Delay}", initialDelay);
        await Task.Delay(initialDelay, stoppingToken);
        await RunOnce(stoppingToken);

        // --- Align next tick to midnight UTC ---
        var toMidnight = TimeUntilNextUtcMidnight();
        logger.LogInformation("DailyReportWorker: waiting {Delay} to next UTC midnight", toMidnight);
        await Task.Delay(toMidnight, stoppingToken);

        // --- Daily loop at fixed period (no drift) ---
        var period = TimeSpan.FromDays(1);
        using var timer = new PeriodicTimer(period);

        // run immediately at midnight
        await RunOnce(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnce(stoppingToken);
        }
    }

        private async Task RunOnce(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<IDailyReportJob>();
            var (count, _) = await job.RunOnceAsync(ct);
            var now = DateTime.UtcNow;
            // small guard so you can see logs quickly
            Console.WriteLine($"[DailyReportWorker] {now:o} sent {count} rows");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DailyReportWorker: job failed");
        }
    }
}