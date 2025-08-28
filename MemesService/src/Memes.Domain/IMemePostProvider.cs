namespace Memes.Domain;

public interface IMemePostProvider
{
    Task<IReadOnlyList<MemePost>> GetTopFromLast24HoursAsync(
        int take = 20, CancellationToken ct = default);
}