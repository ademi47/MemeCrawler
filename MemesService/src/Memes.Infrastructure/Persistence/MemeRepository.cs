using Memes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Memes.Infrastructure.Persistence;

public interface IMemeRepository
{
    Task UpsertWithSnapshotAsync(IEnumerable<MemePost> posts, DateTimeOffset snapshotAt, CancellationToken ct);
    Task<IReadOnlyList<MemeReportItem>> GetTopFromDbLast24hAsync(int take, CancellationToken ct);
}

public sealed class MemeRepository(MemesDbContext db) : IMemeRepository
{
    public async Task UpsertWithSnapshotAsync(IEnumerable<MemePost> posts, DateTimeOffset snapshotAt, CancellationToken ct)
    {
        foreach (var p in posts)
        {
            var existing = await db.Memes.FirstOrDefaultAsync(m => m.RedditId == p.Id, ct);
            if (existing is null)
            {
                existing = new Meme
                {
                    RedditId = p.Id,
                    Title = p.Title,
                    Author = p.Author,
                    Permalink = p.Permalink.ToString(),
                    ContentUrl = p.ContentUrl.ToString(),
                    CreatedUtc = p.CreatedUtc,
                    Thumbnail = p.Thumbnail,
                    FirstSeenUtc = snapshotAt,
                    LastSeenUtc = snapshotAt
                };
                db.Memes.Add(existing);
                await db.SaveChangesAsync(ct); // ensure Id for FK
            }
            else
            {
                // keep latest title/content if they change
                existing.Title = p.Title;
                existing.Author = p.Author;
                existing.Permalink = p.Permalink.ToString();
                existing.ContentUrl = p.ContentUrl.ToString();
                existing.Thumbnail = p.Thumbnail;
                existing.LastSeenUtc = snapshotAt;
            }

            db.MemeSnapshots.Add(new MemeSnapshot
            {
                MemeId = existing.Id,
                Upvotes = p.Upvotes,
                NumComments = p.NumComments,
                SnapshotAt = snapshotAt
            });
        }

        await db.SaveChangesAsync(ct);
    }

   public async Task<IReadOnlyList<MemeReportItem>> GetTopFromDbLast24hAsync(int take, CancellationToken ct)
    {
        take = (take is >= 1 and <= 100) ? take : 20;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);

        // Latest snapshot per meme (no time filter here)
        var latestPerMeme =
            from s in db.MemeSnapshots.AsNoTracking()
            group s by s.MemeId into g
            select new
            {
                MemeId     = g.Key,
                // values from the most recent snapshot
                Upvotes    = g.OrderByDescending(x => x.SnapshotAt).Select(x => x.Upvotes).FirstOrDefault(),
                NumComments= g.OrderByDescending(x => x.SnapshotAt).Select(x => x.NumComments).FirstOrDefault(),
                SnapshotAt = g.Max(x => x.SnapshotAt)
            };

        // Only memes created in last 24h; join to their latest snapshot
        var query =
            from m in db.Memes.AsNoTracking()
            where m.CreatedUtc >= cutoff
            join ls in latestPerMeme on m.Id equals ls.MemeId           // inner join -> requires at least one snapshot
            orderby ls.Upvotes descending, m.CreatedUtc descending
            select new MemeReportItem
            {
                Id          = m.RedditId,
                Title       = m.Title,
                Author      = m.Author,
                Permalink   = m.Permalink,
                ContentUrl  = m.ContentUrl,
                Upvotes     = ls.Upvotes,
                NumComments = ls.NumComments,
                CreatedUtc  = m.CreatedUtc,
                Thumbnail   = m.Thumbnail
            };

        return await query.Take(take).ToListAsync(ct);
    }

}

public sealed class MemeReportItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Author { get; init; }
    public required string Permalink { get; init; }
    public required string ContentUrl { get; init; }
    public required int Upvotes { get; init; }
    public required int NumComments { get; init; }   // <-- add this
    public required DateTimeOffset CreatedUtc { get; init; }
    public string? Thumbnail { get; init; }
}