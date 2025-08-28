using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Memes.Infrastructure.Persistence;

public sealed class MemesDbContextFactory : IDesignTimeDbContextFactory<MemesDbContext>
{
    public MemesDbContext CreateDbContext(string[] args)
    {
        // Build minimal configuration (prefer env var ConnectionStrings__Default)
        var cfg = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var conn = cfg.GetConnectionString("Default")
                   ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                   ?? "Host=localhost;Port=5432;Database=memesdb;Username=memes;Password=memes";

        var opts = new DbContextOptionsBuilder<MemesDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new MemesDbContext(opts);
    }
}