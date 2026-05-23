using BusinessLogic.Employees;
using BusinessLogic.Identity;
using DataAccess;
using DataAccess.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Tests.Presentation.E2E;

/// <summary>
/// Spins up the real ASP.NET Core pipeline against an in-memory SQLite database.
/// Swaps the production JWT bearer scheme for a header-driven test scheme so
/// each test can claim any role just by setting a request header (see
/// <see cref="TestAuthClientExtensions"/>). The schema is created once per
/// factory; <see cref="ResetAsync"/> wipes data between tests.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // A single persistent connection keeps the :memory: database alive for the
    // entire lifetime of the factory — SQLite drops an in-memory database as
    // soon as the last connection to it closes.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    // Per-factory temp directory for uploaded documents so the API's own
    // uploads folder is never written to from tests.
    private readonly string _uploadsPath =
        Path.Combine(Path.GetTempPath(), "pm-api-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = _uploadsPath,
                // Suppress the production seed accounts — tests own seeding.
                ["Identity:Seed:Accounts"] = null
            });
        });

        // ConfigureTestServices runs after the application's own service
        // registrations, so we can safely replace the production DbContext.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_connection));

            // Strip the JWT bearer handlers and wire a header-driven scheme in
            // their place. The fallback policy from Program.cs still applies
            // — tests that omit the header get a 401, which is what we want
            // for the anonymous-access assertions.
            services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();
            services.RemoveAll<IConfigureOptions<JwtBearerOptions>>();
            services.RemoveAll<IPostConfigureOptions<JwtBearerOptions>>();

            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Point the fallback policy at the test scheme — the production
            // build pinned it to JwtBearer specifically.
            services.Configure<AuthorizationOptions>(o =>
            {
                o.FallbackPolicy = new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();
                o.DefaultPolicy = new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();
            });
        });
    }

    /// <summary>
    /// Creates the schema on first call (EnsureCreated is idempotent), truncates
    /// all tables so the next test starts with an empty database, and re-seeds
    /// the three Identity roles. Roles seed once per Reset because Identity
    /// queries them on every login/role check.
    /// </summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Delete in FK-safe order: documents and junctions first.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserRoles");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserClaims");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserLogins");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserTokens");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUsers");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetRoleClaims");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetRoles");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ProjectTasks");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ProjectDocuments");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ProjectEmployees");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Projects");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Employees");

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.AllList)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
    }

    /// <summary>
    /// Provisions an employee + linked Identity user with the given role. The
    /// account uses a strong password so Identity's PasswordValidator stays
    /// out of the way. Returns the new employee id — the role-aware tests
    /// thread this back into the <c>X-Test-EmployeeId</c> header so resource
    /// checks resolve to the same row.
    /// </summary>
    public async Task<int> SeedUserAsync(
        string firstName,
        string lastName,
        string email,
        string role,
        CancellationToken ct = default)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var employee = new Employee(firstName, lastName, null);
        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmployeeId = employee.Id,
            EmailConfirmed = true
        };
        var create = await userManager.CreateAsync(user, "Test#12345");
        if (!create.Succeeded)
            throw new InvalidOperationException(
                "Seed user creation failed: " +
                string.Join("; ", create.Errors.Select(e => e.Description)));

        var addRole = await userManager.AddToRoleAsync(user, role);
        if (!addRole.Succeeded)
            throw new InvalidOperationException(
                "Seed role assignment failed: " +
                string.Join("; ", addRole.Errors.Select(e => e.Description)));

        return employee.Id;
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
