using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Memes.Infrastructure.Persistence;

namespace Memes.Infrastructure.Reporting;

public class DailyReportGenerator
{
    public byte[] GenerateReport(IEnumerable<MemeReportItem> memes, DateTimeOffset reportDate)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(12));
                
                page.Header()
                    .Text($"Daily Meme Report – {reportDate:yyyy-MM-dd}")
                    .FontSize(18).Bold().AlignCenter();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);   // Rank
                        columns.RelativeColumn(3);    // Title
                        columns.RelativeColumn(2);    // Author
                        columns.ConstantColumn(60);   // Upvotes
                        columns.ConstantColumn(60);   // Comments
                    });

                    // Header row
                    table.Header(header =>
                    {
                        header.Cell().Text("#").Bold();
                        header.Cell().Text("Title").Bold();
                        header.Cell().Text("Author").Bold();
                        header.Cell().Text("Upvotes").Bold();
                        header.Cell().Text("Comments").Bold();
                    });

                    int rank = 1;
                    foreach (var meme in memes)
                    {
                        table.Cell().Text(rank++);
                        table.Cell().Text(meme.Title).FontSize(10);
                        table.Cell().Text(meme.Author);
                        table.Cell().Text(meme.Upvotes.ToString("N0"));
                        table.Cell().Text(meme.NumComments.ToString("N0"));
                    }
                });

                page.Footer()
                    .AlignRight()
                    .Text($"Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            });
        });

        return doc.GeneratePdf();
    }
}
