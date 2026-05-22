using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tests.Presentation.Web.E2E;

namespace Tests.Presentation.Web.Controllers;

/// <summary>
/// E2E tests for the /employees Razor pages. Boots the real MVC pipeline
/// against an in-memory SQLite database and submits real form posts with
/// valid antiforgery tokens, so every test exercises the full stack:
/// routing → model binding → antiforgery → MediatR → handler → EF Core →
/// view rendering / redirect.
/// </summary>
public class EmployeesPagesTests(WebFactory factory)
    : IClassFixture<WebFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        // Inspect the 302 from POSTs ourselves instead of silently following it.
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
        string email      = "ivan@example.com")
    {
        var token = await _client.FetchTokenAsync("/employees/create", Ct);
        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  firstName),
            new KeyValuePair<string, string?>("LastName",   lastName),
            new KeyValuePair<string, string?>("Patronymic", patronymic),
            new KeyValuePair<string, string?>("Email",      email),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);

        // Location is "/employees/{id}" — pull the trailing int.
        var path = resp.Headers.Location!.ToString();
        return int.Parse(path[(path.LastIndexOf('/') + 1)..]);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // The index page must render and reflect the data currently in the database.
    [Fact]
    public async Task Index_ListsCreatedEmployees()
    {
        await CreateEmployeeAsync(lastName: "Sidorov", email: "sidorov@example.com");

        var html = await (await _client.GetAsync("/employees", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains("Sidorov",             html);
        Assert.Contains("sidorov@example.com", html);
    }

    // The happy path — valid form data redirects to the detail page of the
    // newly created employee and the record can be fetched back.
    [Fact]
    public async Task CreateEmployee_ValidForm_RedirectsToDetail()
    {
        var id = await CreateEmployeeAsync(email: "happy@example.com");

        var detail = await _client.GetAsync($"/employees/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var html = await detail.Content.ReadAsStringAsync(Ct);
        Assert.Contains("happy@example.com", html);
    }

    // Required-field validation must round-trip through ModelState: the
    // response is the form re-rendered (200), not a redirect, and contains
    // the specific message from the DataAnnotation.
    [Fact]
    public async Task CreateEmployee_MissingLastName_ReturnsFormWithValidationError()
    {
        var token = await _client.FetchTokenAsync("/employees/create", Ct);

        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  "Ivan"),
            new KeyValuePair<string, string?>("LastName",   ""),
            new KeyValuePair<string, string?>("Patronymic", "S"),
            new KeyValuePair<string, string?>("Email",      "ivan@example.com"),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Last name is required", html);
    }

    // The duplicate-email guard lives in the command handler, surfacing as a
    // DomainValidationException. The controller must catch it and re-render
    // the form with the message attached to ModelState — not redirect.
    [Fact]
    public async Task CreateEmployee_DuplicateEmail_ReturnsFormWithDomainError()
    {
        await CreateEmployeeAsync(email: "dup@example.com");

        var token = await _client.FetchTokenAsync("/employees/create", Ct);
        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  "Other"),
            new KeyValuePair<string, string?>("LastName",   "Person"),
            new KeyValuePair<string, string?>("Patronymic", "X"),
            new KeyValuePair<string, string?>("Email",      "dup@example.com"),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync(Ct);
        // Domain-level message mentions the duplicated email.
        Assert.Contains("dup@example.com", html);
    }

    // Editing must persist new values and redirect back to Detail; an absent
    // (unchanged) field on the controller would still take the new value
    // since the entire form is posted.
    [Fact]
    public async Task EditEmployee_ValidForm_PersistsChangesAndRedirectsToDetail()
    {
        var id = await CreateEmployeeAsync(
            firstName: "Ivan", lastName: "Petrov", email: "ivan@example.com");

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

    // Edit and Detail must both 404 for a non-existent id so the user is
    // not silently dropped onto an empty form or detail page.
    [Fact]
    public async Task EditAndDetail_NonExistentId_Return404()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/employees/99999",      Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/employees/99999/edit", Ct)).StatusCode);
    }

    // Delete is a POST (anti-CSRF). After it, the employee must really be
    // gone — the next Detail GET should 404.
    [Fact]
    public async Task DeleteEmployee_RedirectsToIndexAndRemovesRecord()
    {
        var id = await CreateEmployeeAsync(email: "tobedeleted@example.com");

        // Token is fetched off the Index page where the delete button lives.
        var token = await _client.FetchTokenAsync("/employees", Ct);
        var resp = await _client.PostFormAsync(
            $"/employees/{id}/delete", Array.Empty<KeyValuePair<string, string?>>(), token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/employees", resp.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/employees/{id}", Ct)).StatusCode);
    }

    // The /employees/search AJAX endpoint must return JSON usable by the
    // autocomplete dropdowns — partial last-name match, capped by `limit`.
    [Fact]
    public async Task SearchEmployees_ByPartialLastName_ReturnsJsonMatches()
    {
        await CreateEmployeeAsync(lastName: "Sidorov",   email: "sidorov@example.com");
        await CreateEmployeeAsync(lastName: "Sidorenko", email: "sidorenko@example.com");
        await CreateEmployeeAsync(lastName: "Petrov",    email: "petrov@example.com");

        var resp = await _client.GetAsync("/employees/search?term=Sidor&limit=10", Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var matches = await resp.Content.ReadFromJsonAsync<SearchHit[]>(Json, Ct);
        Assert.Equal(2, matches!.Length);
        Assert.All(matches, m => Assert.Contains("Sidor", m.FullName,
            StringComparison.OrdinalIgnoreCase));
    }

    private record SearchHit(int Id, string FullName);
}
