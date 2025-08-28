using Memes.Application;
using Memes.Domain;
using Memes.Infrastructure;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.EntityFrameworkCore;
using Memes.Infrastructure.Persistence;
using Memes.Infrastructure.Background;
using Memes.Infrastructure.Reporting;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();               // <-- required for Docker logs
builder.Logging.SetMinimumLevel(LogLevel.Information);

var token  = builder.Configuration["Telegram:BotToken"];
var chatId = builder.Configuration["Telegram:ChatId"];
var jitterBackoff = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);

if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
{
    builder.Services.AddSingleton<ITelegramReportSender, NullTelegramReportSender>();
}
else
{
    builder.Services.AddSingleton<ITelegramReportSender>(_ => new TelegramReportSender(token!, chatId!));
}

// HttpClient with basic resilience

builder.Services.AddHttpClient<RedditClient>()
    .AddResilienceHandler("reddit", builder =>
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(1),
            ShouldHandle = args =>
                ValueTask.FromResult(
                    args.Outcome.Result is { } r && ((int)r.StatusCode == 429 ||
                    (int)r.StatusCode >= 500) || args.Outcome.Exception is not null)
        })
        .AddPolicyHandler(Policy<HttpResponseMessage>
        .HandleResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500)
        .WaitAndRetryAsync(jitterBackoff));
    });
    


//QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

var allowFrontend = "_allowFrontend";
builder.Services.AddCors(o => o.AddPolicy(allowFrontend, p =>
    p.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));



// Add DbContext
builder.Services.AddDbContext<MemesDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npg => npg.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null)));

// Repo + worker
builder.Services.AddScoped<IMemeRepository, MemeRepository>();
builder.Services.AddHostedService<MemeCrawlWorker>();

builder.Services.AddScoped<IMemePostProvider, RedditClient>();
builder.Services.Configure<RedditOptions>(builder.Configuration.GetSection("Reddit"));
builder.Services.AddScoped<GetTopMemesLast24h>();

//Daily Report
builder.Services.AddScoped<DailyReportGenerator>();
// after your other services:
builder.Services.AddScoped<Memes.Infrastructure.Reporting.IDailyReportJob,
                           Memes.Infrastructure.Reporting.DailyReportJob>();


// configure Telegram
builder.Services.AddSingleton(sp => 
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var token  = cfg["Telegram:BotToken"] ?? throw new InvalidOperationException("Telegram:BotToken missing");
    var chatId = cfg["Telegram:ChatId"]   ?? throw new InvalidOperationException("Telegram:ChatId missing");
    return new TelegramReportSender(token, chatId);
}
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors(allowFrontend);

// Apply migrations automatically on startup (convenience; optional)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MemesDbContext>();

    var attempts = 0;
    const int maxAttempts = 10;
    var delay = TimeSpan.FromSeconds(3);

    while (true)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempts < maxAttempts)
        {
            attempts++;
            app.Logger.LogWarning(ex, "DB not ready (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                attempts, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

// Health
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Use case endpoint
app.MapGet("/memes/top-24h", async (int? take, GetTopMemesLast24h uc, CancellationToken ct) =>
{
    var list = await uc.HandleAsync(take ?? 20, ct);
    // shape a lightweight DTO for clients
    var payload = list.Select(m => new {
        m.Id,
        m.Title,
        m.Author,
        Permalink = m.Permalink.ToString(),
        ContentUrl = m.ContentUrl.ToString(),
        m.Upvotes,
        m.NumComments,
        CreatedUtc = m.CreatedUtc,
        m.Thumbnail
    });

    return Results.Ok(payload);
})
.WithName("GetTopMemesLast24h")
.Produces(StatusCodes.Status200OK)
.WithOpenApi();

// NEW: DB-backed report
app.MapGet("/reports/top-24h", async (int? take, IMemeRepository repo, CancellationToken ct) =>
{
    var list = await repo.GetTopFromDbLast24hAsync(take is >=1 and <=100 ? take.Value : 20, ct);
    return Results.Ok(list);
})
.WithName("GetTopMemesReportLast24h")
.Produces(StatusCodes.Status200OK)
.WithOpenApi();

app.MapPost("/reports/run-daily-now", async (
    [FromServices] IDailyReportJob job,   // <-- force DI
    CancellationToken ct) =>
{
    var (count, _) = await job.RunOnceAsync(ct);
    return Results.Ok(new { sent = true, count });
})
.WithName("RunDailyReportNow");

//To test manual test endpoint for pdf sending
app.MapPost("/reports/send-telegram-now", async (
    IMemeRepository repo,
    DailyReportGenerator generator,
    ITelegramReportSender sender,
    ILoggerFactory lf,
    CancellationToken ct) =>
{
    var log = lf.CreateLogger("SendTelegramNow");
    try
    {
        var items = await repo.GetTopFromDbLast24hAsync(20, ct);   // may be empty; still build PDF
        var pdf   = generator.GenerateReport(items, DateTimeOffset.UtcNow);

        await sender.SendPdfReport(pdf,
            $"MemeReport-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf",
            $"Top memes (last 24h) • {DateTime.UtcNow:yyyy-MM-dd}",
            ct);

        log.LogInformation("PDF sent: {Count} rows", items.Count);
        return Results.Ok(new { sent = true, count = items.Count });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Telegram send failed");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/debug/send-text", async (ITelegramReportSender sender, CancellationToken ct) =>
{
    await sender.SendPdfReport(new byte[0], "empty.pdf", "Hello from MemeService", ct);
    return Results.Ok();
});

app.Run();