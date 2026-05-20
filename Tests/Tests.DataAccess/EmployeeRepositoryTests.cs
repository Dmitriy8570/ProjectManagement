using BusinessLogic.Employees;
using BusinessLogic.Projects;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Tests.DataAccess;

public class EmployeeRepositoryTests : DatabaseTestBase
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private Employee Make(string first, string last, string patronymic = "X", string? email = null) =>
        new(first, last, patronymic, email ?? $"{first.ToLower()}.{last.ToLower()}@x.com");

    private async Task<Employee> AddAsync(Employee e)
    {
        Db.Employees.Add(e);
        await Db.SaveChangesAsync(Ct);
        return e;
    }

    // ── SearchEmployeesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SearchEmployeesAsync_NullTerm_ReturnsAllOrderedByLastName()
    {
        await AddAsync(Make("Bob",   "Zeta"));
        await AddAsync(Make("Alice", "Alpha"));
        await AddAsync(Make("Carol", "Mu"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(null, 100, Ct);

        Assert.Equal(["Alpha", "Mu", "Zeta"], result.Select(e => e.LastName));
    }

    [Fact]
    public async Task SearchEmployeesAsync_WhitespaceTerm_ReturnsAll()
    {
        await AddAsync(Make("A", "A"));
        await AddAsync(Make("B", "B"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("   ", 100, Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchEmployeesAsync_MatchesByFirstName()
    {
        await AddAsync(Make("Ivan",  "Petrov"));
        await AddAsync(Make("Maria", "Ivanova"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("Ivan", 100, Ct);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchEmployeesAsync_MatchesByLastName()
    {
        await AddAsync(Make("Alice", "Smith"));
        await AddAsync(Make("Bob",   "Jones"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("Smith", 100, Ct);

        Assert.Single(result);
        Assert.Equal("Alice", result[0].FirstName);
    }

    [Fact]
    public async Task SearchEmployeesAsync_MatchesByPatronymic()
    {
        await AddAsync(Make("A", "A", patronymic: "Nikolaevich"));
        await AddAsync(Make("B", "B", patronymic: "Petrovich"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("Nikolaevich", 100, Ct);

        Assert.Single(result);
        Assert.Equal("A", result[0].FirstName);
    }

    [Fact]
    public async Task SearchEmployeesAsync_MatchesByEmail()
    {
        await AddAsync(Make("A", "A", email: "unique@corp.com"));
        await AddAsync(Make("B", "B", email: "other@corp.com"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("unique", 100, Ct);

        Assert.Single(result);
        Assert.Equal("unique@corp.com", result[0].Email);
    }

    [Fact]
    public async Task SearchEmployeesAsync_IsCaseInsensitive()
    {
        await AddAsync(Make("Alice", "Smith"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync("alice", 100, Ct);

        Assert.Single(result);
    }

    [Fact]
    public async Task SearchEmployeesAsync_EscapesPercentWildcard()
    {
        await AddAsync(Make("A", "A", email: "a@x.com"));
        await AddAsync(Make("B", "B", email: "b@x.com"));

        var sut = new EmployeeRepository(Db);

        // "%" alone would match everything if not escaped
        var result = await sut.SearchEmployeesAsync("%", 100, Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchEmployeesAsync_RespectsLimit()
    {
        for (var i = 1; i <= 5; i++)
            await AddAsync(Make($"F{i}", $"L{i}"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(null, 3, Ct);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task SearchEmployeesAsync_OrdersByLastNameThenFirstName()
    {
        await AddAsync(Make("Zara",  "Smith"));
        await AddAsync(Make("Alice", "Smith"));
        await AddAsync(Make("Bob",   "Adams"));

        var sut = new EmployeeRepository(Db);

        var result = await sut.SearchEmployeesAsync(null, 100, Ct);

        Assert.Equal(["Bob", "Alice", "Zara"], result.Select(e => e.FirstName));
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

    // ── EmailExistsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task EmailExistsAsync_ReturnsTrue_WhenEmailTaken()
    {
        await AddAsync(Make("A", "A", email: "taken@x.com"));

        var sut = new EmployeeRepository(Db);

        Assert.True(await sut.EmailExistsAsync("taken@x.com", null, Ct));
    }

    [Fact]
    public async Task EmailExistsAsync_ReturnsFalse_WhenEmailFree()
    {
        var sut = new EmployeeRepository(Db);

        Assert.False(await sut.EmailExistsAsync("free@x.com", null, Ct));
    }

    [Fact]
    public async Task EmailExistsAsync_IsCaseInsensitive()
    {
        await AddAsync(Make("A", "A", email: "User@X.COM"));

        var sut = new EmployeeRepository(Db);

        Assert.True(await sut.EmailExistsAsync("user@x.com", null, Ct));
    }

    [Fact]
    public async Task EmailExistsAsync_ExcludesSpecifiedId()
    {
        var emp = await AddAsync(Make("A", "A", email: "same@x.com"));

        var sut = new EmployeeRepository(Db);

        // Same employee editing their own email — should not report a conflict.
        Assert.False(await sut.EmailExistsAsync("same@x.com", emp.Id, Ct));
    }

    [Fact]
    public async Task EmailExistsAsync_ReturnsTrueForOtherEmployee_WhenExcludingDifferentId()
    {
        var emp1 = await AddAsync(Make("A", "A", email: "taken@x.com"));
        var emp2 = await AddAsync(Make("B", "B", email: "other@x.com"));

        var sut = new EmployeeRepository(Db);

        // emp2 tries to take emp1's email — should still be a conflict.
        Assert.True(await sut.EmailExistsAsync("taken@x.com", emp2.Id, Ct));
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
