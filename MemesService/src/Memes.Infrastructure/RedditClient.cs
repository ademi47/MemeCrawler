using System.Net.Http.Json;
using System.Text.Json;
using Memes.Domain;

namespace Memes.Infrastructure;

// Sample: JSON listing: https://www.reddit.com/r/memes/top.json?t=day&limit=20
// Note: Reddit requires a real User-Agent; we also add basic retry at the handler level.
public sealed class RedditClient(HttpClient http) : IMemePostProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<MemePost>> GetTopFromLast24HoursAsync(
        int take = 20, CancellationToken ct = default)
    {
        var url = $"https://www.reddit.com/r/memes/top.json?t=day&limit={take}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("MemesService/1.0 (by u/examplebot)");

        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var root = await res.Content.ReadFromJsonAsync<RedditListingRoot>(JsonOpts, ct)
                   ?? throw new InvalidOperationException("Invalid reddit response");

        var items = root.Data?.Children ?? [];
        var mapped = new List<MemePost>(items.Count);

        foreach (var ch in items)
        {
            var d = ch.Data;
            if (d is null) continue;

            // construct absolute URLs
            var permalink = new Uri($"https://www.reddit.com{d.permalink}");
            var contentUrl = Uri.TryCreate(d.url_overridden_by_dest ?? d.url ?? "", UriKind.Absolute, out var cu)
                ? cu : permalink;

            mapped.Add(new MemePost
            {
                Id = d.id,
                Title = d.title ?? "",
                Author = d.author ?? "unknown",
                Permalink = permalink,
                ContentUrl = contentUrl,
                Upvotes = d.ups,
                NumComments = d.num_comments,
                CreatedUtc = DateTimeOffset.FromUnixTimeSeconds((long)d.created_utc),
                Thumbnail = d.thumbnail
            });
        }

        // Filter up to last 24h
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        return mapped
            .Where(m => m.CreatedUtc >= cutoff)
            .OrderByDescending(m => m.Upvotes)
            .ThenByDescending(m => m.CreatedUtc)
            .Take(take)
            .ToList();
    }

    // Reddit DTOs
    private sealed class RedditListingRoot { public RedditListingData? Data { get; set; } }
    private sealed class RedditListingData { public List<RedditChild> Children { get; set; } = []; }
    private sealed class RedditChild { public RedditPost? Data { get; set; } }
    private sealed class RedditPost
    {
        public string id { get; set; } = "";
        public string? title { get; set; }
        public string? author { get; set; }
        public string? url { get; set; }
        public string? url_overridden_by_dest { get; set; }
        public string? permalink { get; set; }
        public string? thumbnail { get; set; }
        public int ups { get; set; }
        public int num_comments { get; set; }
        public double created_utc { get; set; }
    }
}
