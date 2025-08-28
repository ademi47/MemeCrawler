using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memes.Infrastructure.Reporting;

public sealed class TelegramReportSender : ITelegramReportSender
{
    private readonly ITelegramBotClient _bot;
    private readonly ChatId _chatId;

    public TelegramReportSender(string botToken, string chatId)
    {
        _bot = new TelegramBotClient(botToken);
        _chatId = new ChatId(chatId); // user id or -100... group id
    }

    public async Task SendPdfReport(byte[] pdfBytes, string filename, string caption, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(pdfBytes);
        var file = InputFile.FromStream(stream, fileName: filename);

        await _bot.SendDocumentAsync(
            chatId: _chatId,
            document: file,
            caption: caption,
            cancellationToken: ct
        );
    }
}