// ITelegramReportSender.cs
public interface ITelegramReportSender
{
    Task SendPdfReport(byte[] pdf, string filename, string caption, CancellationToken ct = default);
}

// TelegramReportSender implements ITelegramReportSender (your existing real sender)
