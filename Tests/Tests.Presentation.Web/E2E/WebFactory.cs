using BusinessLogic.Employees;
using BusinessLogic.Identity;
using DataAccess;
using DataAccess.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

namespace Tests.Presentation.Web.E2E;

/// <summary>
/// Boots the real Razor MVC pipeline against an in-memory SQLite database and
/// swaps the production Identity cookie scheme for a header-driven test
/// scheme. Each test class clones a base <see cref="HttpClient"/> and stamps
/// the role header on it to drive role-aware behavior.
/// </summary>
public sealed class WebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

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
                ["FileStorage:BasePath"] = _uploadsPath,
                // Tests own seeding — block the prod seed accounts from showing up.
                ["Identity:Seed:Accounts"] = null
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_connection));

            // Replace the cookie / Identity scheme with the header-based test
            // scheme so tests can claim any role without a real login.
            services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();
            services.RemoveAll<IConfigureOptions<CookieAuthenticationOptions>>();
            services.RemoveAll<IPostConfigureOptions<CookieAuthenticationOptions>>();

            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

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
    /// Truncates all data tables and re-seeds the three Identity roles so
    /// each test starts with a clean slate. ResetAsync runs in the test
    /// class's InitializeAsync.
    /// </summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

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
