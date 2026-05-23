using BusinessLogic.Employees;
using BusinessLogic.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccess.Identity;

/// <summary>
/// On first start, ensures the three application roles exist and that the
/// demo accounts listed under <c>Identity:Seed:Accounts</c> in configuration
/// are provisioned. Idempotent — safe to run on every startup.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.AllList)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var config = sp.GetRequiredService<IConfiguration>();
        var accounts = config.GetSection("Identity:Seed:Accounts").Get<SeedAccount[]>() ?? Array.Empty<SeedAccount>();

        if (accounts.Length == 0)
            return;

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<AppDbContext>();

        foreach (var seed in accounts)
        {
            if (string.IsNullOrWhiteSpace(seed.Email) || string.IsNullOrWhiteSpace(seed.Password))
                continue;

            if (await userManager.FindByEmailAsync(seed.Email) is not null)
                continue;

            if (!Roles.AllList.Contains(seed.Role))
                throw new InvalidOperationException(
                    $"Seed account '{seed.Email}' references unknown role '{seed.Role}'. " +
                    $"Expected one of: {string.Join(", ", Roles.AllList)}.");

            // Each seeded account needs a backing Employee row because
            // ApplicationUser.EmployeeId is a required FK. Names come from
            // configuration but fall back to a sensible default — humans can
            // edit them in /employees/{id}/edit after first login.
            var employee = new Employee(
                firstName: seed.FirstName ?? "Demo",
                lastName: seed.LastName ?? seed.Role,
                patronymic: null);
            db.Employees.Add(employee);
            await db.SaveChangesAsync(ct);

            var user = new ApplicationUser
            {
                UserName = seed.Email,
                Email = seed.Email,
                EmployeeId = employee.Id,
                EmailConfirmed = true
            };
            var create = await userManager.CreateAsync(user, seed.Password);
            if (!create.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to seed account '{seed.Email}': " +
                    string.Join("; ", create.Errors.Select(e => e.Description)));

            await userManager.AddToRoleAsync(user, seed.Role);
        }
    }

    /// <summary>
    /// Plain DTO bound from configuration — sits at the bottom because it's
    /// only used by the binding above. Keeping it private would force a
    /// separate file; nested is the minimum-noise option.
    /// </summary>
    private sealed class SeedAccount
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
