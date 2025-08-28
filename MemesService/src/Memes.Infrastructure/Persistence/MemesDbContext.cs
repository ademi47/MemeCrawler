using Microsoft.EntityFrameworkCore;

namespace Memes.Infrastructure.Persistence;

public sealed class MemesDbContext(DbContextOptions<MemesDbContext> options) : DbContext(options)
{
    public DbSet<Meme> Memes => Set<Meme>();
    public DbSet<MemeSnapshot> MemeSnapshots => Set<MemeSnapshot>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Meme>(e =>
        {
            e.ToTable("memes");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RedditId).IsUnique();
            e.Property(x => x.RedditId).IsRequired();
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.Author).HasMaxLength(100);
            e.Property(x => x.Permalink).IsRequired();
            e.Property(x => x.ContentUrl).IsRequired();
        });

        b.Entity<MemeSnapshot>(e =>
        {
            e.ToTable("meme_snapshots");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MemeId, x.SnapshotAt });
            e.HasOne(x => x.Meme)
             .WithMany(x => x.Snapshots)
             .HasForeignKey(x => x.MemeId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public sealed class Meme
{
    public int Id { get; set; }
    public required string RedditId { get; set; }
    public required string Title { get; set; }
    public required string Author { get; set; }
    public required string Permalink { get; set; }
    public required string ContentUrl { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string? Thumbnail { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }

    public List<MemeSnapshot> Snapshots { get; set; } = [];
}

public sealed class MemeSnapshot
{
    public int Id { get; set; }
    public int MemeId { get; set; }
    public Meme Meme { get; set; } = null!;
    public int Upvotes { get; set; }
    public int NumComments { get; set; }
    public DateTimeOffset SnapshotAt { get; set; }
}