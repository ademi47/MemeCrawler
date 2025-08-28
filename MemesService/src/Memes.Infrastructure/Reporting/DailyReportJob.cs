using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Memes.Domain; 
using Memes.Infrastructure.Persistence;
using Memes.Infrastructure.Persistence;
using Memes.Infrastructure.Reporting;

namespace Memes.Infrastructure.Reporting
{
    public interface IDailyReportJob
    {
        Task<(int count, byte[]? pdf)> RunOnceAsync(CancellationToken ct);
    }

    public sealed class DailyReportJob : IDailyReportJob
    {
        private readonly IMemeRepository _repo;
        private readonly DailyReportGenerator _generator;
        private readonly ITelegramReportSender _sender;
        private readonly ILogger<DailyReportJob> _logger;

        public DailyReportJob(
            IMemeRepository repo,
            DailyReportGenerator generator,
            ITelegramReportSender sender,
            ILogger<DailyReportJob> logger)
        {
            _repo = repo;
            _generator = generator;
            _sender = sender;
            _logger = logger;
        }

        public async Task<(int count, byte[]? pdf)> RunOnceAsync(CancellationToken ct)
        {
            var memes = await _repo.GetTopFromDbLast24hAsync(20, ct);
            if (memes.Count == 0)
                _logger.LogWarning("DailyReportJob: no memes found in last 24h");

            var pdf = _generator.GenerateReport(memes, DateTimeOffset.UtcNow);

            await _sender.SendPdfReport(
                pdf,
                $"MemeReport-{DateTime.UtcNow:yyyyMMdd}.pdf",
                $"Top memes report for {DateTime.UtcNow:yyyy-MM-dd}",
                ct);

            _logger.LogInformation("DailyReportJob: sent {Count} rows to Telegram", memes.Count);
            return (memes.Count, pdf);
        }
    }
}