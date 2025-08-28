using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Memes.Domain;

namespace Memes.Infrastructure;

// Options bound from configuration
public sealed class RedditOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string UserAgent { get; set; } = "MemeCrawler/1.0 (by u:yourusername)";
}

// Simple in-memory token cache (fix: `sealed`, not `filesealed`)
sealed class RedditTokenCache
{
    public string? AccessToken { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; } = DateTimeOffset.MinValue;
    public bool IsValid() =>
        !string.IsNullOrEmpty(AccessToken) && DateTimeOffset.UtcNow < ExpiresAtUtc.AddSeconds(-60);
}

// Uses OAuth + UA and implements IMemePostProvider
public sealed class RedditClient : IMemePostProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;
    private readonly RedditOptions _opt;
    private readonly RedditTokenCache _cache = new();

    public RedditClient(HttpClient http, IOptions<RedditOptions> opt)
    {
        _http = http;
        _opt = opt.Value;

        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(_opt.UserAgent);
    }

    public async Task<IReadOnlyList<MemePost>> GetTopFromLast24HoursAsync(int take = 20, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://oauth.reddit.com/r/memes/top?t=day&limit={take}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.UserAgent.ParseAdd(_opt.UserAgent);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var root = await res.Content.ReadFromJsonAsync<RedditListingRoot>(JsonOpts, ct)
                   ?? throw new InvalidOperationException("Invalid reddit response");

        var items = root.Data?.Children ?? [];
        var mapped = new List<MemePost>(items.Count);

        foreach (var ch in items)
        {
            var d = ch.Data;
            if (d is null) continue;

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

        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        return mapped
            .Where(m => m.CreatedUtc >= cutoff)
            .OrderByDescending(m => m.Upvotes)
            .ThenByDescending(m => m.CreatedUtc)
            .Take(take)
            .ToList();
    }

    // OAuth password grant (script app)
    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cache.IsValid()) return _cache.AccessToken!;

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opt.ClientId}:{_opt.ClientSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Headers.UserAgent.ParseAdd(_opt.UserAgent);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = _opt.Username,
            ["password"] = _opt.Password
        });

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        var token = json.GetProperty("access_token").GetString()!;
        var expires = json.TryGetProperty("expires_in", out var e)
            ? TimeSpan.FromSeconds(e.GetInt32())
            : TimeSpan.FromMinutes(45);

        _cache.AccessToken = token;
        _cache.ExpiresAtUtc = DateTimeOffset.UtcNow.Add(expires);
        return token;
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