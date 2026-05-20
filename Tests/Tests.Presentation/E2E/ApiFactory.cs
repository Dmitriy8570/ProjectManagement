using DataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tests.Presentation.E2E;

/// <summary>
/// Spins up the real ASP.NET Core pipeline against an in-memory SQLite database.
/// One factory instance is shared across all tests in a test class via IClassFixture;
/// ResetAsync() clears data between tests so each one starts with a clean slate
/// while the schema is only created once.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // A single persistent connection keeps the :memory: database alive for the
    // entire lifetime of the factory — SQLite drops an in-memory database as
    // soon as the last connection to it closes.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        // ConfigureTestServices runs after the application's own service
        // registrations, so we can safely replace the production DbContext.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Creates the schema on first call (EnsureCreated is idempotent) and
    /// truncates all tables so the next test starts with an empty database.
    /// </summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Delete in FK-safe order: junction first, then the two entity tables.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ProjectEmployees");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Projects");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Employees");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _connection.Dispose();
        base.Dispose(disposing);
    }
}
