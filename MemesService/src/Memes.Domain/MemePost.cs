namespace Memes.Domain;

public sealed class MemePost
{
    public required string Id { get; init; }               // reddit thing id
    public required string Title { get; init; }
    public required string Author { get; init; }
    public required Uri Permalink { get; init; }           // full reddit url
    public required Uri ContentUrl { get; init; }          // image/gif/external
    public required int Upvotes { get; init; }
    public required int NumComments { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public string? Thumbnail { get; init; }
}