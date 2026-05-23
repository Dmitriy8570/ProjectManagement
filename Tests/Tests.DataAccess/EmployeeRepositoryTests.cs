using BusinessLogic.Employees;
using BusinessLogic.Identity;
using BusinessLogic.Projects;
using DataAccess.Identity;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Tests.DataAccess;

public class EmployeeRepositoryTests : DatabaseTestBase
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private Employee Make(string first, string last, string patronymic = "X") =>
        new(first, last, patronymic);

    private async Task<Employee> AddAsync(Employee e)
    {
        Db.Employees.Add(e);
        await Db.SaveChangesAsync(Ct);
        return e;
    }

    // Seeds an AspNetUsers row linked to an employee so search/role tests can
    // exercise the EF join against Users — Email moved off Employee onto the
    // linked ApplicationUser.
    private async Task<ApplicationUser> AddUserAsync(
        Employee employee, string email, string? role = null)
    {
        var normalized = email.ToUpperInvariant();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            NormalizedUserName = normalized,
            Email = email,
            NormalizedEmail = normalized,
            EmployeeId = employee.Id,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        Db.Users.Add(user);

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleEntity = await Db.Roles
                .FirstOrDefaultAsync(r => r.Name == role, Ct);
            if (roleEntity is null)
            {
                roleEntity = new IdentityRole(role)
                {
                    Id = Guid.NewGuid().ToString(),
                    NormalizedName = role.ToUpperInvariant()
                };
                Db.Roles.Add(roleEntity);
            }
            Db.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = user.Id,
                RoleId = roleEntity.Id
            });
        }

        await Db.SaveChangesAsync(Ct);
        return user;
    }

    // ── SearchEmployeesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SearchEmployeesAsync_NullTerm_ReturnsAllOrderedByLastName()
    {
        await AddAsync(Make("Bob",   "Zeta"));
        await AddAsync(Make("Alice", "Alpha"));
        await AddAsync(Make("Carol", "Mu"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(null, 100, null, Ct);

        Assert.Equal(["Alpha", "Mu", "Zeta"], result.Select(e => e.LastName));
    }

    [Fact]
    public async Task SearchEmployeesAsync_WhitespaceTerm_ReturnsAll()
    {
        await AddAsync(Make("A", "A"));
        await AddAsync(Make("B", "B"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("   ", 100, null, Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchEmployeesAsync_MatchesByFirstName()
    {
        await AddAsync(Make("Ivan",  "Petrov"));
        await AddAsync(Make("Maria", "Ivanova"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("Ivan", 100, null, Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchEmployeesAsync_MatchesByLastName()
    {
        await AddAsync(Make("Alice", "Smith"));
        await AddAsync(Make("Bob",   "Jones"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("Smith", 100, null, Ct);

        Assert.Single(result);
        Assert.Equal("Alice", result[0].FirstName);
    }

    [Fact]
    public async Task SearchEmployeesAsync_MatchesByPatronymic()
    {
        await AddAsync(Make("A", "A", patronymic: "Nikolaevich"));
        await AddAsync(Make("B", "B", patronymic: "Petrovich"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("Nikolaevich", 100, null, Ct);

        Assert.Single(result);
        Assert.Equal("A", result[0].FirstName);
    }

    [Fact]
    public async Task SearchEmployeesAsync_MatchesByEmail()
    {
        var e1 = await AddAsync(Make("A", "A"));
        var e2 = await AddAsync(Make("B", "B"));
        await AddUserAsync(e1, "unique@corp.com");
        await AddUserAsync(e2, "other@corp.com");

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("unique", 100, null, Ct);

        Assert.Single(result);
        Assert.Equal("unique@corp.com", result[0].Email);
    }

    [Fact]
    public async Task SearchEmployeesAsync_IsCaseInsensitive()
    {
        await AddAsync(Make("Alice", "Smith"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("alice", 100, null, Ct);

        Assert.Single(result);
    }

    [Fact]
    public async Task SearchEmployeesAsync_EscapesPercentWildcard()
    {
        var e1 = await AddAsync(Make("A", "A"));
        var e2 = await AddAsync(Make("B", "B"));
        await AddUserAsync(e1, "a@x.com");
        await AddUserAsync(e2, "b@x.com");

        var sut = new EmployeeRepository(Db);

        // "%" alone would match everything if not escaped
        var result = await sut.SearchEmployeesAsync("%", 100, null, Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchEmployeesAsync_RespectsLimit()
    {
        for (var i = 1; i <= 5; i++)
            await AddAsync(Make($"F{i}", $"L{i}"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(null, 3, null, Ct);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task SearchEmployeesAsync_OrdersByLastNameThenFirstName()
    {
        await AddAsync(Make("Zara",  "Smith"));
        await AddAsync(Make("Alice", "Smith"));
        await AddAsync(Make("Bob",   "Adams"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(null, 100, null, Ct);

        Assert.Equal(["Bob", "Alice", "Zara"], result.Select(e => e.FirstName));
    }

    [Fact]
    public async Task SearchEmployeesAsync_EmployeeWithoutLinkedUser_StillReturnedWithEmptyEmail()
    {
        // The repository does a LEFT JOIN to AspNetUsers so an employee in a
        // transient state without a linked account doesn't silently disappear.
        await AddAsync(Make("Lonely", "Worker"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(null, 100, null, Ct);

        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Email);
    }

    // ── SearchEmployeesAsync — role filter ───────────────────────────────────

    [Fact]
    public async Task SearchEmployeesAsync_RoleFilter_OnlyReturnsEmployeesInListedRoles()
    {
        // Three employees: a Director, a ProjectManager, and a plain Employee.
        // Filtering by Director+ProjectManager must hide the plain Employee.
        var director = await AddAsync(Make("D", "Director"));
        var pm = await AddAsync(Make("P", "Manager"));
        var emp = await AddAsync(Make("E", "Worker"));

        await AddUserAsync(director, "director@x.com", Roles.Director);
        await AddUserAsync(pm, "pm@x.com", Roles.ProjectManager);
        await AddUserAsync(emp, "emp@x.com", Roles.Employee);

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(
            null, 100, new[] { Roles.Director, Roles.ProjectManager }, Ct);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Email == "director@x.com");
        Assert.Contains(result, e => e.Email == "pm@x.com");
        Assert.DoesNotContain(result, e => e.Email == "emp@x.com");
    }

    [Fact]
    public async Task SearchEmployeesAsync_RoleFilter_ExcludesEmployeesWithoutAccount()
    {
        // No user account => no role => must be filtered out.
        var pm = await AddAsync(Make("P", "Manager"));
        await AddAsync(Make("L", "Lonely"));   // no linked user
        await AddUserAsync(pm, "pm@x.com", Roles.ProjectManager);

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(
            null, 100, new[] { Roles.ProjectManager }, Ct);

        Assert.Single(result);
        Assert.Equal("pm@x.com", result[0].Email);
    }

    [Fact]
    public async Task SearchEmployeesAsync_RoleFilter_IsCaseInsensitive()
    {
        var pm = await AddAsync(Make("P", "Manager"));
        await AddUserAsync(pm, "pm@x.com", Roles.ProjectManager);

        var sut = new EmployeeRepository(Db);

        // Lower-cased role name still matches via NormalizedName.
        var result = await sut.SearchEmployeesAsync(null, 100, new[] { "projectmanager" }, Ct);

        Assert.Single(result);
    }

    [Fact]
    public async Task SearchEmployeesAsync_EmptyRoleFilter_BehavesAsUnfiltered()
    {
        var pm = await AddAsync(Make("P", "Manager"));
        await AddAsync(Make("L", "Lonely"));
        await AddUserAsync(pm, "pm@x.com", Roles.ProjectManager);

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(null, 100, Array.Empty<string>(), Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchEmployeesAsync_RoleFilter_CombinesWithTerm()
    {
        var d = await AddAsync(Make("Anna",   "Director"));
        var pm = await AddAsync(Make("Anna",  "Manager"));
        var emp = await AddAsync(Make("Anna", "Worker"));
        await AddUserAsync(d, "d@x.com", Roles.Director);
        await AddUserAsync(pm, "pm@x.com", Roles.ProjectManager);
        await AddUserAsync(emp, "e@x.com", Roles.Employee);

        var sut = new EmployeeRepository(Db);

        // Term matches all three by first name, but role filter narrows to PMs.
        var result = await sut.SearchEmployeesAsync(
            "Anna", 100, new[] { Roles.ProjectManager }, Ct);

        Assert.Single(result);
        Assert.Equal("pm@x.com", result[0].Email);
    }

    // ── GetEmployeeByIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetEmployeeByIdAsync_ReturnsEmployee_WhenFound()
    {
        var emp = await AddAsync(Make("Alice", "Smith"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.GetEmployeeByIdAsync(emp.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(emp.Id, result!.Id);
    }

    [Fact]
    public async Task GetEmployeeByIdAsync_ReturnsNull_WhenNotFound()
    {
        var sut = new EmployeeRepository(Db);

        var result = await sut.GetEmployeeByIdAsync(999, Ct);

        Assert.Null(result);
    }

    // ── GetEmployeeDtoByIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetEmployeeDtoByIdAsync_ProjectsEmailFromLinkedUser()
    {
        var emp = await AddAsync(Make("Alice", "Smith"));
        await AddUserAsync(emp, "alice@x.com");

        var sut = new EmployeeRepository(Db);

        var dto = await sut.GetEmployeeDtoByIdAsync(emp.Id, Ct);

        Assert.NotNull(dto);
        Assert.Equal("alice@x.com", dto!.Email);
    }

    [Fact]
    public async Task GetEmployeeDtoByIdAsync_NoLinkedUser_ReturnsDtoWithEmptyEmail()
    {
        var emp = await AddAsync(Make("Alice", "Smith"));

        var sut = new EmployeeRepository(Db);

        var dto = await sut.GetEmployeeDtoByIdAsync(emp.Id, Ct);

        Assert.NotNull(dto);
        Assert.Equal(string.Empty, dto!.Email);
    }

    // ── GetEmployeesByIdsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetEmployeesByIdsAsync_ReturnsMatchingEmployees()
    {
        var e1 = await AddAsync(Make("A", "A"));
        var e2 = await AddAsync(Make("B", "B"));
        await AddAsync(Make("C", "C"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.GetEmployeesByIdsAsync([e1.Id, e2.Id], Ct);

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Contains(e.Id, new[] { e1.Id, e2.Id }));
    }

    [Fact]
    public async Task GetEmployeesByIdsAsync_ReturnsEmpty_WhenIdsEmpty()
    {
        await AddAsync(Make("A", "A"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.GetEmployeesByIdsAsync([], Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEmployeesByIdsAsync_IgnoresMissingIds()
    {
        var emp = await AddAsync(Make("A", "A"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.GetEmployeesByIdsAsync([emp.Id, 999], Ct);

        Assert.Single(result);
        Assert.Equal(emp.Id, result[0].Id);
    }

    // ── AddEmployeeAsync / SaveAsync ──────────────────────────────────────────

    [Fact]
    public async Task AddEmployeeAsync_PersistsAfterSave()
    {
        var sut = new EmployeeRepository(Db);
        var emp = Make("New", "Employee");

        await sut.AddEmployeeAsync(emp, Ct);
        await sut.SaveAsync(Ct);

        Assert.NotEqual(0, emp.Id);
        Assert.NotNull(await Db.Employees.FindAsync([emp.Id], Ct));
    }

    // ── DeleteEmployeeAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEmployeeAsync_ReturnsTrueAndRemovesEmployee()
    {
        var emp = await AddAsync(Make("A", "A"));

        var sut = new EmployeeRepository(Db);

        var deleted = await sut.DeleteEmployeeAsync(emp.Id, Ct);

        // ExecuteDeleteAsync bypasses the EF change tracker, so we must go
        // straight to the database to verify the row is gone.
        var stillExists = await Db.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Id == emp.Id, Ct);
        Assert.True(deleted);
        Assert.False(stillExists);
    }

    [Fact]
    public async Task DeleteEmployeeAsync_ReturnsFalse_WhenNotFound()
    {
        var sut = new EmployeeRepository(Db);

        var deleted = await sut.DeleteEmployeeAsync(999, Ct);

        Assert.False(deleted);
    }

    // ── IsProjectManagerAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task IsProjectManagerAsync_ReturnsTrue_WhenEmployeeIsProjectManager()
    {
        var pm = await AddAsync(Make("PM", "PM"));
        Db.Projects.Add(new Project("P", "c", "e", new(2024, 1, 1), new(2024, 12, 31), pm, 1));
        await Db.SaveChangesAsync(Ct);

        var sut = new EmployeeRepository(Db);

        Assert.True(await sut.IsProjectManagerAsync(pm.Id, Ct));
    }

    [Fact]
    public async Task IsProjectManagerAsync_ReturnsFalse_WhenEmployeeIsNotProjectManager()
    {
        var emp = await AddAsync(Make("A", "A"));

        var sut = new EmployeeRepository(Db);

        Assert.False(await sut.IsProjectManagerAsync(emp.Id, Ct));
    }
}
