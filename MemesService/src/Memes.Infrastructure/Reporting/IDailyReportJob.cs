public interface IDailyReportJob
{
    Task<(int count, byte[]? pdf)> RunOnceAsync(CancellationToken ct);
}
