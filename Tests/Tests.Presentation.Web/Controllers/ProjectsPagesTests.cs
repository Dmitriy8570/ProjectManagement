using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessLogic.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Tests.Presentation.Web.E2E;

namespace Tests.Presentation.Web.Controllers;

/// <summary>
/// E2E tests for the /projects Razor pages — list/filter/pagination, the
/// create-and-edit form roundtrips, team membership commands, and the
/// project-document upload/download/delete cycle. Role-aware: covers the
/// spec rule that PMs only see their own projects and employees see only
/// ones they participate in.
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

    // Director-created employee. Defaults to the ProjectManager role so the
    // returned id is immediately usable as a PM on a project.
    private async Task<int> CreateEmployeeAsync(
        string lastName,
        string email,
        string role = Roles.ProjectManager)
    {
        _client.AsDirector();
        var token = await _client.FetchTokenAsync("/employees/create", Ct);
        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  "Test"),
            new KeyValuePair<string, string?>("LastName",   lastName),
            new KeyValuePair<string, string?>("Patronymic", "Testovich"),
            new KeyValuePair<string, string?>("Email",      email),
            new KeyValuePair<string, string?>("Password",   "Test#12345"),
            new KeyValuePair<string, string?>("Role",       role),
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
        foreach (var id in employeeIds ?? Array.Empty<int>())
            fields.Add(new("EmployeeIds", id.ToString()));

        _client.AsDirector();
        var token = await _client.FetchTokenAsync("/projects/create", Ct);
        var resp = await _client.PostFormAsync("/projects/create", fields, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var location = resp.Headers.Location!.ToString();
        return int.Parse(location[(location.LastIndexOf('/') + 1)..]);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ListsCreatedProjects()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        await CreateProjectAsync(pmId, name: "Alpha Project");

        _client.AsDirector();
        var html = await (await _client.GetAsync("/projects", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains("Alpha Project", html);
    }

    [Fact]
    public async Task Index_FilterByName_NarrowsResults()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        await CreateProjectAsync(pmId, name: "Alpha Project");
        await CreateProjectAsync(pmId, name: "Beta Initiative");

        _client.AsDirector();
        // Flush the TempData success banner from the last create — its text
        // contains the project name and would otherwise pollute substring asserts.
        await _client.GetAsync("/projects", Ct);

        var html = await (await _client.GetAsync("/projects?NameSearch=Alpha", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains   ("Alpha Project",   html);
        Assert.DoesNotContain("Beta Initiative", html);
    }

    [Fact]
    public async Task Index_Pagination_ReturnsOnlyPageWindow()
    {
        var pmId  = await CreateEmployeeAsync("Manager", "pm@example.com");
        var alpha = await CreateProjectAsync(pmId, name: "Alpha Project");
        var beta  = await CreateProjectAsync(pmId, name: "Beta Initiative");

        _client.AsDirector();
        var page1 = await (await _client.GetAsync(
            "/projects?Page=1&PageSize=1", Ct)).Content.ReadAsStringAsync(Ct);
        var page2 = await (await _client.GetAsync(
            "/projects?Page=2&PageSize=1", Ct)).Content.ReadAsStringAsync(Ct);

        var alphaLink = $"/projects/{alpha}";
        var betaLink  = $"/projects/{beta}";

        Assert.NotEqual(page1.Contains(alphaLink), page2.Contains(alphaLink));
        Assert.NotEqual(page1.Contains(betaLink),  page2.Contains(betaLink));
    }

    [Fact]
    public async Task CreateProject_ValidForm_RedirectsToDetailAndPersistsTeam()
    {
        var pmId     = await CreateEmployeeAsync("Manager", "pm@example.com");
        var teammate = await CreateEmployeeAsync("Worker",  "worker@example.com", Roles.Employee);

        var id = await CreateProjectAsync(
            pmId, name: "Team Project", employeeIds: new[] { teammate });

        _client.AsDirector();
        var html = await (await _client.GetAsync($"/projects/{id}", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains("Team Project", html);
        Assert.Contains("Worker",       html);
        Assert.Contains("Manager",      html);
    }

    [Fact]
    public async Task CreateProject_MissingName_ReturnsFormWithValidationError()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");

        _client.AsDirector();
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

    [Fact]
    public async Task EditProject_ValidForm_PersistsChangesAndRedirectsToDetail()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        var id   = await CreateProjectAsync(pmId, name: "Original Name");

        _client.AsDirector();
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

    [Fact]
    public async Task EditAndDetail_NonExistentId_Return404()
    {
        _client.AsDirector();
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/projects/99999",      Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/projects/99999/edit", Ct)).StatusCode);
    }

    [Fact]
    public async Task AssignAndUnassign_TogglesEmployeeOnProjectTeam()
    {
        var pmId      = await CreateEmployeeAsync("Manager",   "pm@example.com");
        var workerId  = await CreateEmployeeAsync("Newcomer",  "newcomer@example.com", Roles.Employee);
        var projectId = await CreateProjectAsync(pmId, name: "Membership Project");

        _client.AsDirector();
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

    [Fact]
    public async Task DeleteProject_RedirectsToIndexAndRemovesRecord()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        var id   = await CreateProjectAsync(pmId);

        _client.AsDirector();
        var token = await _client.FetchTokenAsync("/projects", Ct);
        var resp = await _client.PostFormAsync(
            $"/projects/{id}/delete", Array.Empty<KeyValuePair<string, string?>>(), token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/projects", resp.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/projects/{id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task DocumentLifecycle_UploadDownloadDelete()
    {
        var pmId      = await CreateEmployeeAsync("Manager", "pm@example.com");
        var projectId = await CreateProjectAsync(pmId);

        _client.AsDirector();
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

        var detailHtml = await (await _client.GetAsync($"/projects/{projectId}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.Contains("notes.txt", detailHtml);

        var docId = ExtractDocumentId(detailHtml, projectId);

        var dl = await _client.GetAsync(
            $"/projects/{projectId}/documents/{docId}/download", Ct);
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
        Assert.Equal(bytes, await dl.Content.ReadAsByteArrayAsync(Ct));

        var deleteToken = await _client.FetchTokenAsync($"/projects/{projectId}", Ct);
        var delResp = await _client.PostFormAsync(
            $"/projects/{projectId}/documents/{docId}/delete",
            Array.Empty<KeyValuePair<string, string?>>(), deleteToken, Ct);
        Assert.Equal(HttpStatusCode.Redirect, delResp.StatusCode);

        var htmlAfter = await (await _client.GetAsync($"/projects/{projectId}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("notes.txt", htmlAfter);
    }

    [Fact]
    public async Task SearchProjects_ByPartialName_ReturnsJsonMatches()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        await CreateProjectAsync(pmId, name: "Alpha Project");
        await CreateProjectAsync(pmId, name: "Beta Initiative");

        _client.AsDirector();
        var resp = await _client.GetAsync("/projects/search?term=Alpha", Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var hits = await resp.Content.ReadFromJsonAsync<SearchHit[]>(Json, Ct);
        Assert.Single(hits!);
        Assert.Equal("Alpha Project", hits![0].Name);
    }

    // ── Role-based authorization ─────────────────────────────────────────────

    [Fact]
    public async Task Index_Anonymous_Returns401()
    {
        _client.AsAnonymous();
        var resp = await _client.GetAsync("/projects", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // PM Index narrows automatically to their projects — Index controller
    // mutates the filter for non-Directors.
    [Fact]
    public async Task Index_AsProjectManager_OnlyShowsOwnedProjects()
    {
        var pmAId = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmBId = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var pAId = await CreateProjectAsync(pmAId, name: "Alpha Project");
        var pBId = await CreateProjectAsync(pmBId, name: "Beta Initiative");

        _client.AsProjectManager(pmAId);
        var html = await (await _client.GetAsync("/projects", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains($"/projects/{pAId}", html);
        Assert.DoesNotContain($"/projects/{pBId}", html);
    }

    // Сотрудник Index narrows to projects they participate in.
    [Fact]
    public async Task Index_AsEmployee_OnlyShowsParticipantProjects()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        var empId = await factory.SeedUserAsync("Reg", "Worker", "reg@local", Roles.Employee, Ct);
        var inProjectId = await CreateProjectAsync(pmId, name: "I belong here", employeeIds: new[] { empId });
        var outsiderProjectId = await CreateProjectAsync(pmId, name: "I do not belong here");

        _client.AsEmployee(empId);
        var html = await (await _client.GetAsync("/projects", Ct))
            .Content.ReadAsStringAsync(Ct);

        Assert.Contains($"/projects/{inProjectId}", html);
        Assert.DoesNotContain($"/projects/{outsiderProjectId}", html);
    }

    // Direct Detail URL on someone else's project must 403, even for a PM.
    [Fact]
    public async Task Detail_AsProjectManagerOfDifferentProject_Returns403()
    {
        var pmAId = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmBId = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var pBId = await CreateProjectAsync(pmBId, name: "B's Project");

        _client.AsProjectManager(pmAId);
        var resp = await _client.GetAsync($"/projects/{pBId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Сотрудник viewing someone's unrelated project must also 403.
    [Fact]
    public async Task Detail_AsNonParticipantEmployee_Returns403()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        var empId = await factory.SeedUserAsync("Reg", "Worker", "reg@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, name: "X");

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync($"/projects/{projectId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Сотрудник participating in a project can open its detail page.
    [Fact]
    public async Task Detail_AsParticipantEmployee_Returns200()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        var empId = await factory.SeedUserAsync("Reg", "Worker", "reg@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, name: "I belong here", employeeIds: new[] { empId });

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync($"/projects/{projectId}", Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // The Create form is Director-only — a PM stepping in is forbidden.
    [Fact]
    public async Task CreateForm_AsProjectManager_Returns403()
    {
        var pmId = await factory.SeedUserAsync("P", "M", "pm@local", Roles.ProjectManager, Ct);
        _client.AsProjectManager(pmId);

        var resp = await _client.GetAsync("/projects/create", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Delete is Director-only — owning PMs cannot delete their own projects.
    [Fact]
    public async Task DeleteProject_AsOwningProjectManager_Returns403()
    {
        var pmId = await factory.SeedUserAsync("P", "M", "pm@local", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId);

        _client.AsProjectManager(pmId);
        var token = await _client.FetchTokenAsync($"/projects/{projectId}", Ct);
        var resp = await _client.PostFormAsync(
            $"/projects/{projectId}/delete",
            Array.Empty<KeyValuePair<string, string?>>(), token, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Owning PM can edit their own project — proves the role gate plus the
    // resource check both accept the case.
    [Fact]
    public async Task EditProject_AsOwningProjectManager_PersistsChanges()
    {
        var pmId = await factory.SeedUserAsync("P", "M", "pm@local", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId, name: "Original");

        _client.AsProjectManager(pmId);
        var token = await _client.FetchTokenAsync($"/projects/{projectId}/edit", Ct);
        var resp = await _client.PostFormAsync($"/projects/{projectId}/edit", new[]
        {
            new KeyValuePair<string, string?>("Name",             "Renamed By PM"),
            new KeyValuePair<string, string?>("CustomerCompany",  "Acme"),
            new KeyValuePair<string, string?>("ExecutingCompany", "Sibers"),
            new KeyValuePair<string, string?>("StartDate",        "2026-01-01"),
            new KeyValuePair<string, string?>("EndDate",          "2026-12-31"),
            new KeyValuePair<string, string?>("Priority",         "5"),
            new KeyValuePair<string, string?>("ProjectManagerId", pmId.ToString()),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);

        _client.AsDirector();
        var html = await (await _client.GetAsync($"/projects/{projectId}", Ct))
            .Content.ReadAsStringAsync(Ct);
        Assert.Contains("Renamed By PM", html);
    }

    // …but not on someone else's project.
    [Fact]
    public async Task EditForm_AsProjectManagerOfDifferentProject_Returns403()
    {
        var pmAId = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmBId = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var pBId = await CreateProjectAsync(pmBId, name: "B's Project");

        _client.AsProjectManager(pmAId);
        var resp = await _client.GetAsync($"/projects/{pBId}/edit", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Сотрудник cannot reach the edit form even on a project they participate in.
    [Fact]
    public async Task EditForm_AsParticipantEmployee_Returns403()
    {
        var pmId = await CreateEmployeeAsync("Manager", "pm@example.com");
        var empId = await factory.SeedUserAsync("Reg", "Worker", "reg@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId });

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync($"/projects/{projectId}/edit", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    private record SearchHit(int Id, string Name);

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
