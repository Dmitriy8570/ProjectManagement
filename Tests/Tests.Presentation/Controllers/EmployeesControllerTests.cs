using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Identity;
using Tests.Presentation.E2E;

namespace Tests.Presentation.Controllers;

/// <summary>
/// E2E tests for the /api/employees endpoints. Covers both the happy path
/// (Director — owns the directory) and the role-restricted paths (a plain
/// Employee or ProjectManager must not be able to create / edit / delete
/// employee records).
/// </summary>
public class EmployeesApiTests(ApiFactory factory)
    : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // Minimal ProblemDetails projection — we only care about the title in assertions.
    private record ApiProblem(string? Title, string? Detail);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();
    public ValueTask DisposeAsync()          => ValueTask.CompletedTask;

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<int> CreateEmployeeAsync(
        string firstName  = "Ivan",
        string lastName   = "Petrov",
        string patronymic = "Sergeevich",
        string email      = "ivan@example.com",
        string password   = "Test#12345",
        string role       = Roles.Employee,
        CancellationToken ct = default)
    {
        _client.AsDirector();
        var r = await _client.PostAsJsonAsync("/api/employees",
            new { firstName, lastName, patronymic, email, password, role }, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateEmployeeResponse>(Json, ct))!.Id;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // Director can update; only fields sent in the PUT body change.
    [Fact]
    public async Task UpdateEmployee_OnlyChangedFieldsAreReflectedOnGet()
    {
        var id = await CreateEmployeeAsync(
            firstName: "Ivan", lastName: "Petrov",
            email: "ivan@example.com", ct: Ct);

        _client.AsDirector();
        await _client.PutAsJsonAsync($"/api/employees/{id}", new
        {
            firstName = "Andrey",
            email     = "andrey@example.com"
        }, Ct);

        var dto = await (await _client.GetAsync($"/api/employees/{id}", Ct))
            .Content.ReadFromJsonAsync<EmployeeDto>(Json, Ct);

        Assert.Equal("Andrey",            dto!.FirstName);
        Assert.Equal("Petrov",            dto.LastName);
        Assert.Equal("andrey@example.com", dto.Email);
    }

    [Fact]
    public async Task DeleteEmployee_SubsequentGetReturns404WithResourceNotFoundProblem()
    {
        var id = await CreateEmployeeAsync(email: "gone@example.com", ct: Ct);

        _client.AsDirector();
        await _client.DeleteAsync($"/api/employees/{id}", Ct);

        var response = await _client.GetAsync($"/api/employees/{id}", Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found", problem!.Title);
    }

    // The duplicate-email guard lives inside the command handler, not in model
    // validation — it must surface through DomainExceptionHandler as a 400.
    [Fact]
    public async Task CreateEmployee_DuplicateEmail_Returns400WithDomainValidationProblem()
    {
        await CreateEmployeeAsync(email: "dup@example.com", ct: Ct);

        _client.AsDirector();
        var response = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Other", lastName = "Person",
            patronymic = "X",   email    = "dup@example.com",
            password = "Test#12345", role = Roles.Employee
        }, Ct);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", problem!.Title);
    }

    [Fact]
    public async Task SearchEmployees_ByPartialLastName_ReturnsOnlyMatchingEmployees()
    {
        await CreateEmployeeAsync(lastName: "Sidorov",   email: "sidorov@example.com",   ct: Ct);
        await CreateEmployeeAsync(lastName: "Petrov",    email: "petrov@example.com",    ct: Ct);
        await CreateEmployeeAsync(lastName: "Sidorenko", email: "sidorenko@example.com", ct: Ct);

        // Search is gated to Director+PM — a Director request lists matches.
        _client.AsDirector();
        var employees = await (await _client.GetAsync("/api/employees?term=Sidor&limit=10", Ct))
            .Content.ReadFromJsonAsync<EmployeeDto[]>(Json, Ct);

        Assert.Equal(2, employees!.Length);
        Assert.All(employees, e => Assert.Contains("Sidor", e.LastName,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetEmployee_NonExistentId_Returns404WithResourceNotFoundProblem()
    {
        _client.AsDirector();
        var response = await _client.GetAsync("/api/employees/99999", Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found", problem!.Title);
    }

    // ── Role authorization ───────────────────────────────────────────────────

    // The endpoint requires an authenticated user — calling anonymously must
    // produce a 401 from the fallback policy, not a 200 with data.
    [Fact]
    public async Task ListEmployees_Anonymous_Returns401()
    {
        _client.AsAnonymous();
        var response = await _client.GetAsync("/api/employees?limit=10", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // The directory is owned by Руководитель — a plain Сотрудник must not be
    // able to create employee accounts.
    [Fact]
    public async Task CreateEmployee_AsEmployee_Returns403()
    {
        var empId = await factory.SeedUserAsync("Reg", "User", "reg@x.com", Roles.Employee, Ct);
        _client.AsEmployee(empId);

        var response = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "X", lastName = "Y", patronymic = "Z",
            email = "new@example.com", password = "Test#12345", role = Roles.Employee
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Same rule for a ProjectManager — only Director may create employees.
    [Fact]
    public async Task CreateEmployee_AsProjectManager_Returns403()
    {
        var pmId = await factory.SeedUserAsync("P", "M", "pm@x.com", Roles.ProjectManager, Ct);
        _client.AsProjectManager(pmId);

        var response = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "X", lastName = "Y", patronymic = "Z",
            email = "new@example.com", password = "Test#12345", role = Roles.Employee
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Delete is also Director-only.
    [Fact]
    public async Task DeleteEmployee_AsProjectManager_Returns403()
    {
        var pmId = await factory.SeedUserAsync("P", "M", "pm2@x.com", Roles.ProjectManager, Ct);
        var targetId = await CreateEmployeeAsync(email: "victim@example.com", ct: Ct);

        _client.AsProjectManager(pmId);
        var response = await _client.DeleteAsync($"/api/employees/{targetId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // GET /api/employees/{id} stays open to any authenticated user so the SPA
    // can render names appearing inside project DTOs.
    [Fact]
    public async Task GetEmployee_AsEmployee_Returns200()
    {
        var targetId = await CreateEmployeeAsync(email: "visible@example.com", ct: Ct);
        var empId = await factory.SeedUserAsync("Reg", "User", "reg@x.com", Roles.Employee, Ct);

        _client.AsEmployee(empId);
        var dto = await (await _client.GetAsync($"/api/employees/{targetId}", Ct))
            .Content.ReadFromJsonAsync<EmployeeDto>(Json, Ct);

        Assert.Equal("visible@example.com", dto!.Email);
    }

    // Search is restricted to Director+PM (the project wizard needs it). A
    // plain Сотрудник must be turned away.
    [Fact]
    public async Task SearchEmployees_AsEmployee_Returns403()
    {
        var empId = await factory.SeedUserAsync("Reg", "User", "reg@x.com", Roles.Employee, Ct);
        _client.AsEmployee(empId);

        var response = await _client.GetAsync("/api/employees?limit=10", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // The PM picker on the wizard restricts to Director+ProjectManager roles
    // so a plain Сотрудник never shows up as a selectable PM.
    [Fact]
    public async Task SearchEmployees_RoleFilter_HidesPlainEmployees()
    {
        await factory.SeedUserAsync("D", "Director", "d@local", Roles.Director);
        await factory.SeedUserAsync("P", "Manager", "pm@local", Roles.ProjectManager);
        await factory.SeedUserAsync("E", "Worker", "emp@local", Roles.Employee, Ct);

        _client.AsDirector();
        var results = await (await _client.GetAsync(
                $"/api/employees?limit=50&roles={Roles.Director},{Roles.ProjectManager}", Ct))
            .Content.ReadFromJsonAsync<EmployeeDto[]>(Json, Ct);

        Assert.Equal(2, results!.Length);
        Assert.DoesNotContain(results, e => e.Email == "emp@local");
    }

    // Unknown role tokens in the query must be silently dropped — the server
    // never executes a filter the user wasn't allowed to ask for.
    [Fact]
    public async Task SearchEmployees_UnknownRoleInFilter_BehavesAsUnfiltered()
    {
        await factory.SeedUserAsync("D", "Director", "d@local", Roles.Director);
        await factory.SeedUserAsync("E", "Worker", "emp@local", Roles.Employee, Ct);

        _client.AsDirector();
        var results = await (await _client.GetAsync(
                "/api/employees?limit=50&roles=Hacker", Ct))
            .Content.ReadFromJsonAsync<EmployeeDto[]>(Json, Ct);

        Assert.Equal(2, results!.Length);
    }

    // Director can mint a new ProjectManager — the freshly created account is
    // then eligible to be appointed as a PM on a project.
    [Fact]
    public async Task CreateEmployee_WithProjectManagerRole_AppearsInPmPicker()
    {
        var pmId = await CreateEmployeeAsync(
            firstName: "Manager", lastName: "Person",
            email: "newpm@example.com", role: Roles.ProjectManager, ct: Ct);

        _client.AsDirector();
        var results = await (await _client.GetAsync(
                $"/api/employees?limit=50&roles={Roles.ProjectManager}", Ct))
            .Content.ReadFromJsonAsync<EmployeeDto[]>(Json, Ct);

        Assert.Contains(results!, e => e.Id == pmId);
    }

    // Unknown role on creation is rejected by the handler.
    [Fact]
    public async Task CreateEmployee_UnknownRole_Returns400()
    {
        _client.AsDirector();
        var response = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "X", lastName = "Y", patronymic = "Z",
            email = "x@x.com", password = "Test#12345", role = "Hacker"
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
