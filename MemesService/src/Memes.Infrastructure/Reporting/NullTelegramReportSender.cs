using Microsoft.Extensions.Logging; 
namespace Memes.Infrastructure.Reporting;
public sealed class NullTelegramReportSender : ITelegramReportSender
{
    private readonly ILogger<NullTelegramReportSender> _log;

    public NullTelegramReportSender(ILogger<NullTelegramReportSender> log) => _log = log;

    public Task SendPdfReport(byte[] _, string __, string ___, CancellationToken ____)
    {
        _log.LogWarning("Telegram disabled: BotToken/ChatId not configured.");
        return Task.CompletedTask;
    }
}
