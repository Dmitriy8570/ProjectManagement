using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessLogic.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Tests.Presentation.Web.E2E;

namespace Tests.Presentation.Web.Controllers;

/// <summary>
/// E2E tests for the /employees Razor pages. The factory swaps the Identity
/// cookie pipeline for a header-driven test scheme so tests claim the role
/// (Director / ProjectManager / Employee) via a request header. Adds
/// role-aware coverage for the directory pages — only Director may manage.
/// </summary>
public class EmployeesPagesTests(WebFactory factory)
    : IClassFixture<WebFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies     = true
    });

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

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
        string role       = Roles.Employee)
    {
        _client.AsDirector();
        var token = await _client.FetchTokenAsync("/employees/create", Ct);
        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  firstName),
            new KeyValuePair<string, string?>("LastName",   lastName),
            new KeyValuePair<string, string?>("Patronymic", patronymic),
            new KeyValuePair<string, string?>("Email",      email),
            new KeyValuePair<string, string?>("Password",   password),
            new KeyValuePair<string, string?>("Role",       role),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);

        // Location is "/employees/{id}" — pull the trailing int.
        var path = resp.Headers.Location!.ToString();
        return int.Parse(path[(path.LastIndexOf('/') + 1)..]);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ListsCreatedEmployees()
    {
        await CreateEmployeeAsync(lastName: "Sidorov", email: "sidorov@example.com");

        _client.AsDirector();
        var html = await (await _client.GetAsync("/employees", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains("Sidorov",             html);
        Assert.Contains("sidorov@example.com", html);
    }

    [Fact]
    public async Task CreateEmployee_ValidForm_RedirectsToDetail()
    {
        var id = await CreateEmployeeAsync(email: "happy@example.com");

        _client.AsDirector();
        var detail = await _client.GetAsync($"/employees/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var html = await detail.Content.ReadAsStringAsync(Ct);
        Assert.Contains("happy@example.com", html);
    }

    [Fact]
    public async Task CreateEmployee_MissingLastName_ReturnsFormWithValidationError()
    {
        _client.AsDirector();
        var token = await _client.FetchTokenAsync("/employees/create", Ct);

        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  "Ivan"),
            new KeyValuePair<string, string?>("LastName",   ""),
            new KeyValuePair<string, string?>("Patronymic", "S"),
            new KeyValuePair<string, string?>("Email",      "ivan@example.com"),
            new KeyValuePair<string, string?>("Password",   "Test#12345"),
            new KeyValuePair<string, string?>("Role",       Roles.Employee),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Last name is required", html);
    }

    [Fact]
    public async Task CreateEmployee_DuplicateEmail_ReturnsFormWithDomainError()
    {
        await CreateEmployeeAsync(email: "dup@example.com");

        _client.AsDirector();
        var token = await _client.FetchTokenAsync("/employees/create", Ct);
        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  "Other"),
            new KeyValuePair<string, string?>("LastName",   "Person"),
            new KeyValuePair<string, string?>("Patronymic", "X"),
            new KeyValuePair<string, string?>("Email",      "dup@example.com"),
            new KeyValuePair<string, string?>("Password",   "Test#12345"),
            new KeyValuePair<string, string?>("Role",       Roles.Employee),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync(Ct);
        // Domain-level message mentions the duplicated email.
        Assert.Contains("dup@example.com", html);
    }

    [Fact]
    public async Task EditEmployee_ValidForm_PersistsChangesAndRedirectsToDetail()
    {
        var id = await CreateEmployeeAsync(
            firstName: "Ivan", lastName: "Petrov", email: "ivan@example.com");

        _client.AsDirector();
        var token = await _client.FetchTokenAsync($"/employees/{id}/edit", Ct);
        var resp = await _client.PostFormAsync($"/employees/{id}/edit", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  "Andrey"),
            new KeyValuePair<string, string?>("LastName",   "Petrov"),
            new KeyValuePair<string, string?>("Patronymic", "Sergeevich"),
            new KeyValuePair<string, string?>("Email",      "andrey@example.com"),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal($"/employees/{id}", resp.Headers.Location!.ToString());

        var html = await (await _client.GetAsync($"/employees/{id}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.Contains("Andrey",              html);
        Assert.Contains("andrey@example.com",  html);
    }

    [Fact]
    public async Task EditAndDetail_NonExistentId_Return404()
    {
        _client.AsDirector();

        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/employees/99999",      Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/employees/99999/edit", Ct)).StatusCode);
    }

    [Fact]
    public async Task DeleteEmployee_RedirectsToIndexAndRemovesRecord()
    {
        var id = await CreateEmployeeAsync(email: "tobedeleted@example.com");

        _client.AsDirector();
        // Token is fetched off the Index page where the delete button lives.
        var token = await _client.FetchTokenAsync("/employees", Ct);
        var resp = await _client.PostFormAsync(
            $"/employees/{id}/delete", Array.Empty<KeyValuePair<string, string?>>(), token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/employees", resp.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/employees/{id}", Ct)).StatusCode);
    }

    // Search endpoint is reused by the project wizard's autocomplete; Director
    // gets free-text matches by partial last name.
    [Fact]
    public async Task SearchEmployees_ByPartialLastName_ReturnsJsonMatches()
    {
        await CreateEmployeeAsync(lastName: "Sidorov",   email: "sidorov@example.com");
        await CreateEmployeeAsync(lastName: "Sidorenko", email: "sidorenko@example.com");
        await CreateEmployeeAsync(lastName: "Petrov",    email: "petrov@example.com");

        _client.AsDirector();
        var resp = await _client.GetAsync("/employees/search?term=Sidor&limit=10", Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var matches = await resp.Content.ReadFromJsonAsync<SearchHit[]>(Json, Ct);
        Assert.Equal(2, matches!.Length);
        Assert.All(matches, m => Assert.Contains("Sidor", m.FullName,
            StringComparison.OrdinalIgnoreCase));
    }

    // ── Role-based authorization ─────────────────────────────────────────────

    // Anonymous browse hits the fallback policy — must produce 401, not the
    // page contents.
    [Fact]
    public async Task Index_Anonymous_Returns401()
    {
        _client.AsAnonymous();
        var resp = await _client.GetAsync("/employees", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // The directory is Director-only; a plain Сотрудник landing on /employees
    // must be turned away by the role gate.
    [Fact]
    public async Task Index_AsEmployee_Returns403()
    {
        var empId = await factory.SeedUserAsync("Reg", "User", "reg@local", Roles.Employee, Ct);
        _client.AsEmployee(empId);

        var resp = await _client.GetAsync("/employees", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Same rule for ProjectManager — they can only USE the autocomplete, not
    // see the full directory.
    [Fact]
    public async Task Index_AsProjectManager_Returns403()
    {
        var pmId = await factory.SeedUserAsync("P", "M", "pm@local", Roles.ProjectManager, Ct);
        _client.AsProjectManager(pmId);

        var resp = await _client.GetAsync("/employees", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // The Create form GET is Director-only — a PM stepping in via the URL is
    // forbidden.
    [Fact]
    public async Task CreateForm_AsProjectManager_Returns403()
    {
        var pmId = await factory.SeedUserAsync("P", "M", "pm@local", Roles.ProjectManager, Ct);
        _client.AsProjectManager(pmId);

        var resp = await _client.GetAsync("/employees/create", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Detail stays open to any authenticated user so project pages can link
    // to a person's profile.
    [Fact]
    public async Task Detail_AsEmployee_Returns200()
    {
        var targetId = await CreateEmployeeAsync(email: "visible@example.com");
        var empId = await factory.SeedUserAsync("Reg", "User", "reg@local", Roles.Employee, Ct);

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync($"/employees/{targetId}", Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // Search is open to Director + PM since the wizard needs it. A plain
    // Сотрудник probing the AJAX endpoint must be turned away.
    [Fact]
    public async Task Search_AsEmployee_Returns403()
    {
        var empId = await factory.SeedUserAsync("Reg", "User", "reg@local", Roles.Employee, Ct);
        _client.AsEmployee(empId);

        var resp = await _client.GetAsync("/employees/search?term=&limit=10", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Search is available to PMs (wizard's PM picker uses it).
    [Fact]
    public async Task Search_AsProjectManager_Returns200()
    {
        var pmId = await factory.SeedUserAsync("P", "M", "pm@local", Roles.ProjectManager, Ct);
        _client.AsProjectManager(pmId);

        var resp = await _client.GetAsync("/employees/search?term=&limit=10", Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    private record SearchHit(int Id, string FullName);
}
