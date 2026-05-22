using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tests.Presentation.Web.E2E;

namespace Tests.Presentation.Web.Controllers;

/// <summary>
/// E2E tests for the /projects Razor pages — the heart of the application's
/// UI. Covers list/filter/pagination, the create-and-edit form roundtrips,
/// team-membership commands (assign/unassign), and the project-document
/// upload/download/delete cycle.
/// </summary>
public class ProjectsPagesTests(WebFactory factory)
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

    // ── fixtures / helpers ───────────────────────────────────────────────────

    private async Task<int> CreateEmployeeAsync(string lastName, string email)
    {
        var token = await _client.FetchTokenAsync("/employees/create", Ct);
        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  "Test"),
            new KeyValuePair<string, string?>("LastName",   lastName),
            new KeyValuePair<string, string?>("Patronymic", "Testovich"),
            new KeyValuePair<string, string?>("Email",      email),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var location = resp.Headers.Location!.ToString();
        return int.Parse(location[(location.LastIndexOf('/') + 1)..]);
    }

    private async Task<int> CreateProjectAsync(
        int pmId,
        string name = "Test Project",
        IEnumerable<int>? employeeIds = null,
        int priority = 1)
    {
        var fields = new List<KeyValuePair<string, string?>>
        {
            new("Name",             name),
            new("CustomerCompany",  "Acme Inc"),
            new("ExecutingCompany", "Sibers"),
            new("StartDate",        "2026-01-01"),
            new("EndDate",          "2026-12-31"),
            new("Priority",         priority.ToString()),
            new("ProjectManagerId", pmId.ToString()),
        };
        // List binding convention: repeat the key name once per entry.
        foreach (var id in employeeIds ?? Array.Empty<int>())
            fields.Add(new("EmployeeIds", id.ToString()));

        var token = await _client.FetchTokenAsync("/projects/create", Ct);
        var resp = await _client.PostFormAsync("/projects/create", fields, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var location = resp.Headers.Location!.ToString();
        return int.Parse(location[(location.LastIndexOf('/') + 1)..]);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // The root route ("/") and "/projects" must both reach the Index view
    // and render the projects currently in the database.
    [Fact]
    public async Task Index_ListsCreatedProjects()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        await CreateProjectAsync(pmId, name: "Alpha Project");

        var html = await (await _client.GetAsync("/projects", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains("Alpha Project", html);
    }

    // Name filter must apply server-side; only the matching project should
    // appear in the rendered list, and the unrelated one must not.
    [Fact]
    public async Task Index_FilterByName_NarrowsResults()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        await CreateProjectAsync(pmId, name: "Alpha Project");
        await CreateProjectAsync(pmId, name: "Beta Initiative");

        // Flush the TempData success banner from the last create — its text
        // contains the project name and would otherwise pollute substring asserts.
        await _client.GetAsync("/projects", Ct);

        var html = await (await _client.GetAsync("/projects?NameSearch=Alpha", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains   ("Alpha Project",   html);
        Assert.DoesNotContain("Beta Initiative", html);
    }

    // Pagination: with PageSize=1, each project appears on exactly one page —
    // proves the page-size and skip math reach the repository correctly.
    [Fact]
    public async Task Index_Pagination_ReturnsOnlyPageWindow()
    {
        var pmId  = await CreateEmployeeAsync("Manager", "pm@example.com");
        var alpha = await CreateProjectAsync(pmId, name: "Alpha Project");
        var beta  = await CreateProjectAsync(pmId, name: "Beta Initiative");

        // Match against the per-project detail link rendered into each row.
        // That marker is unique to the list itself, so TempData success banners
        // (which mention the project name) can't interfere with the assertion.
        var page1 = await (await _client.GetAsync(
            "/projects?Page=1&PageSize=1", Ct)).Content.ReadAsStringAsync(Ct);
        var page2 = await (await _client.GetAsync(
            "/projects?Page=2&PageSize=1", Ct)).Content.ReadAsStringAsync(Ct);

        var alphaLink = $"/projects/{alpha}";
        var betaLink  = $"/projects/{beta}";

        Assert.NotEqual(page1.Contains(alphaLink), page2.Contains(alphaLink));
        Assert.NotEqual(page1.Contains(betaLink),  page2.Contains(betaLink));
    }

    // The full create wizard happy path: PM is required, optional team members
    // join the project, and the response redirects to the new project's detail.
    [Fact]
    public async Task CreateProject_ValidForm_RedirectsToDetailAndPersistsTeam()
    {
        var pmId      = await CreateEmployeeAsync("Manager", "pm@example.com");
        var teammate  = await CreateEmployeeAsync("Worker",  "worker@example.com");

        var id = await CreateProjectAsync(
            pmId, name: "Team Project", employeeIds: new[] { teammate });

        var html = await (await _client.GetAsync($"/projects/{id}", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains("Team Project", html);
        Assert.Contains("Worker",       html);
        Assert.Contains("Manager",      html);
    }

    // Required-field validation must round-trip through ModelState: blank name
    // re-renders the form (200) with the DataAnnotation error message.
    [Fact]
    public async Task CreateProject_MissingName_ReturnsFormWithValidationError()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");

        var token = await _client.FetchTokenAsync("/projects/create", Ct);
        var resp = await _client.PostFormAsync("/projects/create", new[]
        {
            new KeyValuePair<string, string?>("Name",             ""),
            new KeyValuePair<string, string?>("CustomerCompany",  "Acme"),
            new KeyValuePair<string, string?>("ExecutingCompany", "Sibers"),
            new KeyValuePair<string, string?>("StartDate",        "2026-01-01"),
            new KeyValuePair<string, string?>("EndDate",          "2026-12-31"),
            new KeyValuePair<string, string?>("Priority",         "1"),
            new KeyValuePair<string, string?>("ProjectManagerId", pmId.ToString()),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Project name is required", html);
    }

    // Edit must persist the new values and redirect back to Detail.
    [Fact]
    public async Task EditProject_ValidForm_PersistsChangesAndRedirectsToDetail()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        var id   = await CreateProjectAsync(pmId, name: "Original Name");

        var token = await _client.FetchTokenAsync($"/projects/{id}/edit", Ct);
        var resp = await _client.PostFormAsync($"/projects/{id}/edit", new[]
        {
            new KeyValuePair<string, string?>("Name",             "Renamed Project"),
            new KeyValuePair<string, string?>("CustomerCompany",  "Acme"),
            new KeyValuePair<string, string?>("ExecutingCompany", "Sibers"),
            new KeyValuePair<string, string?>("StartDate",        "2026-01-01"),
            new KeyValuePair<string, string?>("EndDate",          "2026-12-31"),
            new KeyValuePair<string, string?>("Priority",         "5"),
            new KeyValuePair<string, string?>("ProjectManagerId", pmId.ToString()),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal($"/projects/{id}", resp.Headers.Location!.ToString());

        var html = await (await _client.GetAsync($"/projects/{id}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.Contains("Renamed Project",     html);
        Assert.DoesNotContain("Original Name", html);
    }

    // Both Edit form GET and Detail GET must surface a 404 for missing ids —
    // otherwise the user might end up on an empty edit form for a phantom.
    [Fact]
    public async Task EditAndDetail_NonExistentId_Return404()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/projects/99999",      Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/projects/99999/edit", Ct)).StatusCode);
    }

    // Assign then Unassign must cycle the membership cleanly — the project's
    // detail page should reflect each transition.
    [Fact]
    public async Task AssignAndUnassign_TogglesEmployeeOnProjectTeam()
    {
        var pmId      = await CreateEmployeeAsync("Manager",   "pm@example.com");
        var workerId  = await CreateEmployeeAsync("Newcomer",  "newcomer@example.com");
        var projectId = await CreateProjectAsync(pmId, name: "Membership Project");

        var detailToken = await _client.FetchTokenAsync($"/projects/{projectId}", Ct);

        // Assign
        var assignResp = await _client.PostFormAsync(
            $"/projects/{projectId}/assign",
            new[] { new KeyValuePair<string, string?>("employeeId", workerId.ToString()) },
            detailToken, Ct);
        Assert.Equal(HttpStatusCode.Redirect, assignResp.StatusCode);

        var htmlAfterAssign = await (await _client.GetAsync($"/projects/{projectId}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.Contains("Newcomer", htmlAfterAssign);

        // Unassign
        var unassignToken = await _client.FetchTokenAsync($"/projects/{projectId}", Ct);
        var unassignResp = await _client.PostFormAsync(
            $"/projects/{projectId}/unassign",
            new[] { new KeyValuePair<string, string?>("employeeId", workerId.ToString()) },
            unassignToken, Ct);
        Assert.Equal(HttpStatusCode.Redirect, unassignResp.StatusCode);

        var htmlAfterUnassign = await (await _client.GetAsync($"/projects/{projectId}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("Newcomer", htmlAfterUnassign);
    }

    // Delete is POST-only (anti-CSRF). After delete, the project must really
    // be gone — the next Detail GET should 404.
    [Fact]
    public async Task DeleteProject_RedirectsToIndexAndRemovesRecord()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        var id   = await CreateProjectAsync(pmId);

        var token = await _client.FetchTokenAsync("/projects", Ct);
        var resp = await _client.PostFormAsync(
            $"/projects/{id}/delete", Array.Empty<KeyValuePair<string, string?>>(), token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/projects", resp.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/projects/{id}", Ct)).StatusCode);
    }

    // Full document lifecycle through HTTP: multipart upload → detail page
    // mentions the file → download streams identical bytes → delete removes
    // the entry from the detail page.
    [Fact]
    public async Task DocumentLifecycle_UploadDownloadDelete()
    {
        var pmId      = await CreateEmployeeAsync("Manager", "pm@example.com");
        var projectId = await CreateProjectAsync(pmId);

        // Upload
        var detailToken = await _client.FetchTokenAsync($"/projects/{projectId}", Ct);
        var bytes = Encoding.UTF8.GetBytes("hello world");

        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent(detailToken), "__RequestVerificationToken");

            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(fileContent, "files", "notes.txt");

            var uploadResp = await _client.PostAsync(
                $"/projects/{projectId}/documents/upload", form, Ct);
            Assert.Equal(HttpStatusCode.Redirect, uploadResp.StatusCode);
        }

        // Confirm the file is listed on the detail page and capture its id
        // from the download link the view renders for it.
        var detailHtml = await (await _client.GetAsync($"/projects/{projectId}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.Contains("notes.txt", detailHtml);

        var docId = ExtractDocumentId(detailHtml, projectId);

        // Download — bytes must match what we uploaded.
        var dl = await _client.GetAsync(
            $"/projects/{projectId}/documents/{docId}/download", Ct);
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
        Assert.Equal(bytes, await dl.Content.ReadAsByteArrayAsync(Ct));

        // Delete — POST with antiforgery, then the file should be gone.
        var deleteToken = await _client.FetchTokenAsync($"/projects/{projectId}", Ct);
        var delResp = await _client.PostFormAsync(
            $"/projects/{projectId}/documents/{docId}/delete",
            Array.Empty<KeyValuePair<string, string?>>(), deleteToken, Ct);
        Assert.Equal(HttpStatusCode.Redirect, delResp.StatusCode);

        var htmlAfter = await (await _client.GetAsync($"/projects/{projectId}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("notes.txt", htmlAfter);
    }

    // The /projects/search AJAX endpoint must return JSON usable by the
    // autocomplete dropdown — partial name match, capped server-side.
    [Fact]
    public async Task SearchProjects_ByPartialName_ReturnsJsonMatches()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        await CreateProjectAsync(pmId, name: "Alpha Project");
        await CreateProjectAsync(pmId, name: "Beta Initiative");

        var resp = await _client.GetAsync("/projects/search?term=Alpha", Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var hits = await resp.Content.ReadFromJsonAsync<SearchHit[]>(Json, Ct);
        Assert.Single(hits!);
        Assert.Equal("Alpha Project", hits![0].Name);
    }

    private record SearchHit(int Id, string Name);

    // Pulls the document id out of the first download anchor on the detail
    // page — the view renders /projects/{projectId}/documents/{docId}/download.
    private static int ExtractDocumentId(string html, int projectId)
    {
        var marker = $"/projects/{projectId}/documents/";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "No document link found on detail page.");
        var idStart = start + marker.Length;
        var idEnd = html.IndexOf('/', idStart);
        return int.Parse(html.AsSpan(idStart, idEnd - idStart));
    }
}
