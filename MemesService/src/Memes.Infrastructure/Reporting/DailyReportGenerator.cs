using System.Net.Http;
using Memes.Infrastructure.Persistence;
using QuestPDF.Drawing; // if needed for ImageScaling
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Memes.Infrastructure.Reporting;

public class DailyReportGenerator
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public byte[] GenerateReport(IEnumerable<MemeReportItem> memes, DateTimeOffset reportDate)
    {
        // Pre-fetch images so the QuestPDF compose step stays synchronous
        var memeList = memes?.ToList() ?? new List<MemeReportItem>();
        var withImages = new List<(MemeReportItem Meme, byte[]? ImageBytes)>(memeList.Count);

        foreach (var m in memeList)
        {
            byte[]? bytes = null;

            // Try common properties for an image URL. Adjust to our  model fields if different. later I will modify this
            var imageUrl = GetFirstNonEmpty(
                m.GetType().GetProperty("ImageUrl")?.GetValue(m)?.ToString(),
                m.GetType().GetProperty("PreviewUrl")?.GetValue(m)?.ToString(),
                m.GetType().GetProperty("ThumbnailUrl")?.GetValue(m)?.ToString()
            );

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                try
                {
                    // Basic guard:
                    if (!IsNonImagePlaceholder(imageUrl))
                        bytes = Http.GetByteArrayAsync(imageUrl).GetAwaiter().GetResult();
                }
                catch
                {
                    //
                }
            }

            withImages.Add((m, bytes));
        }

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text($"Daily Meme Report – {reportDate:yyyy-MM-dd}")
                    .FontSize(18)
                    .Bold()
                    .AlignCenter();

                // ===== Summary Table =====
                page.Content()
                    .Column(col =>
                    {
                        col.Item()
                            .PaddingBottom(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30); // Rank
                                    columns.RelativeColumn(3); // Title
                                    columns.RelativeColumn(2); // Author
                                    columns.ConstantColumn(60); // Upvotes
                                    columns.ConstantColumn(60); // Comments
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("#").Bold();
                                    header.Cell().Text("Title").Bold();
                                    header.Cell().Text("Author").Bold();
                                    header.Cell().Text("Upvotes").Bold();
                                    header.Cell().Text("Comments").Bold();
                                });

                                int rank = 1;
                                foreach (var (meme, _) in withImages)
                                {
                                    table.Cell().Text(rank++);
                                    table
                                        .Cell()
                                        .Text(GetProp(meme, "Title") ?? string.Empty)
                                        .FontSize(10);
                                    table.Cell().Text(GetProp(meme, "Author") ?? string.Empty);
                                    table.Cell().Text(FormatNumber(GetInt(meme, "Upvotes")));
                                    table.Cell().Text(FormatNumber(GetInt(meme, "NumComments")));
                                }
                            });

                        // ===== Detailed Cards with Images =====
                        col.Item()
                            .PaddingTop(5)
                            .Column(cards =>
                            {
                                cards
                                    .Item()
                                    .PaddingTop(10)
                                    .Text("Detailed Posts")
                                    .FontSize(14)
                                    .Bold()
                                    .Underline();

                                int rank = 1;
                                foreach (var (m, img) in withImages)
                                {
                                    cards
                                        .Item()
                                        .PaddingVertical(8)
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .Padding(10)
                                        .Column(card =>
                                        {
                                            var title = GetProp(m, "Title") ?? "(no title)";
                                            card.Item()
                                                .Text($"{rank}. {title}")
                                                .FontSize(12)
                                                .Bold();
                                            rank++;

                                            var author = GetProp(m, "Author");
                                            var ups = FormatNumber(GetInt(m, "Upvotes"));
                                            var comments = FormatNumber(GetInt(m, "NumComments"));

                                            // style INSIDE the builder
                                            card.Item()
                                                .Text(txt =>
                                                {
                                                    txt.DefaultTextStyle(x =>
                                                        x.FontSize(10)
                                                            .FontColor(Colors.Grey.Darken2)
                                                    );
                                                    if (!string.IsNullOrEmpty(author))
                                                        txt.Span($"u/{author}  •  ");
                                                    txt.Span(
                                                        $"Score: {ups}  •  Comments: {comments}"
                                                    );
                                                });

                                            if (img is { Length: > 0 })
                                            {
                                                card.Item().PaddingTop(6).Image(img);
                                            }

                                            var link = GetFirstNonEmpty(
                                                GetProp(m, "Permalink"),
                                                GetProp(m, "Url"),
                                                GetProp(m, "PostUrl")
                                            );
                                            if (!string.IsNullOrWhiteSpace(link))
                                            {
                                                if (
                                                    link.StartsWith(
                                                        "/r/",
                                                        StringComparison.OrdinalIgnoreCase
                                                    )
                                                )
                                                    link = $"https://www.reddit.com{link}";

                                                card.Item()
                                                    .PaddingTop(6)
                                                    .Text(t =>
                                                    {
                                                        t.Span("Open: ").FontSize(10);
                                                        t.Hyperlink(link, link);
                                                    });
                                            }
                                        });
                                }
                            });
                    });

                // ===== Footer =====
                page.Footer()
                    .AlignRight()
                    .Text($"Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            });
        });

        return doc.GeneratePdf();
    }

    private static string? GetProp(object obj, string name) =>
        obj.GetType().GetProperty(name)?.GetValue(obj)?.ToString();

    private static int GetInt(object obj, string name)
    {
        var val = obj.GetType().GetProperty(name)?.GetValue(obj);
        if (val == null)
            return 0;
        if (val is int i)
            return i;
        if (int.TryParse(val.ToString(), out var p))
            return p;
        return 0;
    }

    private static string FormatNumber(int n) => n.ToString("N0");

    private static string? GetFirstNonEmpty(params string?[] candidates) =>
        candidates?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

    private static bool IsNonImagePlaceholder(string url)
    {
        var u = url?.ToLowerInvariant() ?? "";
        return u is "self" or "default" or "nsfw" || u.EndsWith(".mp4") || u.Contains("v.redd.it");
    }
}
