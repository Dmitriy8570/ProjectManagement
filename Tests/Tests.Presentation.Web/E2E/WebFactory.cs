using DataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tests.Presentation.Web.E2E;

/// <summary>
/// Boots the real Razor MVC pipeline against an in-memory SQLite database.
/// Mirrors Tests.Presentation/E2E/ApiFactory so both presentation layers are
/// exercised the same way: model binding → MediatR → handler → EF Core,
/// plus view rendering and antiforgery for the form POST paths.
/// </summary>
public sealed class WebFactory : WebApplicationFactory<Program>
{
    // A single persistent connection keeps the :memory: database alive for the
    // entire lifetime of the factory — SQLite drops an in-memory database as
    // soon as the last connection to it closes.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    // Per-factory temp directory for uploaded documents, so tests don't pollute
    // the web project's own uploads folder and can be cleaned up on Dispose.
    private readonly string _uploadsPath =
        Path.Combine(Path.GetTempPath(), "pm-web-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = _uploadsPath
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Creates the schema on first call and truncates tables so each test
    /// starts on a clean slate while the schema is built only once.
    /// </summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync("DELETE FROM ProjectDocuments");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ProjectEmployees");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Projects");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Employees");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
            try { if (Directory.Exists(_uploadsPath)) Directory.Delete(_uploadsPath, true); }
            catch { /* best-effort cleanup */ }
        }
        base.Dispose(disposing);
    }
}
