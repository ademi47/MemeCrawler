using Memes.Domain;
namespace Memes.Application;

public sealed class GetTopMemesLast24h(IMemePostProvider provider)
{
    public async Task<IReadOnlyList<MemePost>> HandleAsync(
        int take = 20, CancellationToken ct = default)
    {
        if (take is < 1 or > 100) take = 20;
        var posts = await provider.GetTopFromLast24HoursAsync(take, ct);

        // just-in-case sort (infra already returns sorted desc)
        return posts.OrderByDescending(p => p.Upvotes)
                    .ThenByDescending(p => p.CreatedUtc)
                    .Take(take)
                    .ToList();
    }
}
