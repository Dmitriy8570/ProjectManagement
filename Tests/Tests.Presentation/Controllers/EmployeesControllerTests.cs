using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessLogic.Employees;
using BusinessLogic.Employees.Commands;
using Tests.Presentation.E2E;

namespace Tests.Presentation.Controllers;

/// <summary>
/// E2E tests for the /api/employees endpoints. The factory boots the real
/// ASP.NET Core pipeline against an in-memory SQLite database, so every test
/// exercises the full stack: model binding → MediatR → handler → EF Core.
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
        CancellationToken ct = default)
    {
        var r = await _client.PostAsJsonAsync("/api/employees",
            new { firstName, lastName, patronymic, email }, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateEmployeeResponse>(Json, ct))!.Id;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // Verifies the partial-update semantics: only fields sent in the PUT body
    // should change; the rest must remain as they were after the original POST.
    [Fact]
    public async Task UpdateEmployee_OnlyChangedFieldsAreReflectedOnGet()
    {
        var id = await CreateEmployeeAsync(
            firstName: "Ivan", lastName: "Petrov",
            email: "ivan@example.com", ct: Ct);

        await _client.PutAsJsonAsync($"/api/employees/{id}", new
        {
            firstName = "Andrey",
            email     = "andrey@example.com"
        }, Ct);

        var dto = await (await _client.GetAsync($"/api/employees/{id}", Ct))
            .Content.ReadFromJsonAsync<EmployeeDto>(Json, Ct);

        Assert.Equal("Andrey",            dto!.FirstName);
        Assert.Equal("Petrov",            dto.LastName);       // unchanged
        Assert.Equal("andrey@example.com", dto.Email);
    }

    // After deletion the resource must be gone — not just return an unexpected
    // status but specifically a "Resource not found" problem.
    [Fact]
    public async Task DeleteEmployee_SubsequentGetReturns404WithResourceNotFoundProblem()
    {
        var id = await CreateEmployeeAsync(email: "gone@example.com", ct: Ct);
        await _client.DeleteAsync($"/api/employees/{id}", Ct);

        var response = await _client.GetAsync($"/api/employees/{id}", Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found", problem!.Title);
    }

    // The duplicate-email guard lives inside the command handler, not in model
    // validation — it must surface through DomainExceptionHandler as a 400
    // with the domain-specific "Validation failed" problem title.
    [Fact]
    public async Task CreateEmployee_DuplicateEmail_Returns400WithDomainValidationProblem()
    {
        await CreateEmployeeAsync(email: "dup@example.com", ct: Ct);

        var response = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Other", lastName = "Person",
            patronymic = "X",   email    = "dup@example.com"
        }, Ct);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", problem!.Title);
    }

    // The search endpoint must apply the LIKE filter correctly — partial match
    // on last name finds two employees named "Sidorov" and "Sidorenko" but
    // not "Petrov".
    [Fact]
    public async Task SearchEmployees_ByPartialLastName_ReturnsOnlyMatchingEmployees()
    {
        await CreateEmployeeAsync(lastName: "Sidorov",   email: "sidorov@example.com",   ct: Ct);
        await CreateEmployeeAsync(lastName: "Petrov",    email: "petrov@example.com",    ct: Ct);
        await CreateEmployeeAsync(lastName: "Sidorenko", email: "sidorenko@example.com", ct: Ct);

        var employees = await (await _client.GetAsync("/api/employees?term=Sidor&limit=10", Ct))
            .Content.ReadFromJsonAsync<EmployeeDto[]>(Json, Ct);

        Assert.Equal(2, employees!.Length);
        Assert.All(employees, e => Assert.Contains("Sidor", e.LastName,
            StringComparison.OrdinalIgnoreCase));
    }

    // Trivial by itself but doubles as smoke test for the 404 path without
    // any prior data setup — ensures the handler works on a cold database.
    [Fact]
    public async Task GetEmployee_NonExistentId_Returns404WithResourceNotFoundProblem()
    {
        var response = await _client.GetAsync("/api/employees/99999", Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found", problem!.Title);
    }
}
