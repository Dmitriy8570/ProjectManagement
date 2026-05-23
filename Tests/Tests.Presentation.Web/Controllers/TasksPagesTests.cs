using System.Net;
using BusinessLogic.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Tests.Presentation.Web.E2E;

namespace Tests.Presentation.Web.Controllers;

public class TasksPagesTests(WebFactory factory)
    : IClassFixture<WebFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies     = true
    });

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();
    public ValueTask DisposeAsync()          => ValueTask.CompletedTask;

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<int> CreateEmployeeAsync(
        string lastName, string email, string role = Roles.ProjectManager)
    {
        _client.AsDirector();
        var token = await _client.FetchTokenAsync("/employees/create", Ct);
        var resp = await _client.PostFormAsync("/employees/create", new[]
        {
            new KeyValuePair<string, string?>("FirstName",  "Test"),
            new KeyValuePair<string, string?>("LastName",   lastName),
            new KeyValuePair<string, string?>("Patronymic", "T"),
            new KeyValuePair<string, string?>("Email",      email),
            new KeyValuePair<string, string?>("Password",   "Test#12345"),
            new KeyValuePair<string, string?>("Role",       role),
        }, token, Ct);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var location = resp.Headers.Location!.ToString();
        return int.Parse(location[(location.LastIndexOf('/') + 1)..]);
    }

    private async Task<int> CreateProjectAsync(
        int pmId, string name = "Test Project", IEnumerable<int>? employeeIds = null)
    {
        var fields = new List<KeyValuePair<string, string?>>
        {
            new("Name",             name),
            new("CustomerCompany",  "Acme"),
            new("ExecutingCompany", "Sibers"),
            new("StartDate",        "2026-01-01"),
            new("EndDate",          "2026-12-31"),
            new("Priority",         "1"),
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

    private async Task<int> CreateTaskAsync(
        int projectId, int authorId, int assigneeId, string name = "Test Task")
    {
        _client.AsDirector();
        var token = await _client.FetchTokenAsync($"/tasks/create?projectId={projectId}", Ct);
        var resp = await _client.PostFormAsync("/tasks/create", new[]
        {
            new KeyValuePair<string, string?>("Name",       name),
            new KeyValuePair<string, string?>("Priority",   "1"),
            new KeyValuePair<string, string?>("Status",     "ToDo"),
            new KeyValuePair<string, string?>("ProjectId",  projectId.ToString()),
            new KeyValuePair<string, string?>("AuthorId",   authorId.ToString()),
            new KeyValuePair<string, string?>("AssigneeId", assigneeId.ToString()),
        }, token, Ct);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        var location = resp.Headers.Location!.ToString();
        return int.Parse(location[(location.LastIndexOf('/') + 1)..]);
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_AsDirector_ShowsAllTasks()
    {
        var pmId = await CreateEmployeeAsync("PM", "pm@test.com");
        var projectId = await CreateProjectAsync(pmId);
        await CreateTaskAsync(projectId, pmId, pmId, "Alpha");

        _client.AsDirector();
        var resp = await _client.GetAsync("/tasks", Ct);
        var html = await resp.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("Alpha", html);
    }

    [Fact]
    public async Task Create_AsDirector_RedirectsToDetail()
    {
        var pmId = await CreateEmployeeAsync("PM", "pm@test.com");
        var projectId = await CreateProjectAsync(pmId);

        _client.AsDirector();
        var token = await _client.FetchTokenAsync($"/tasks/create?projectId={projectId}", Ct);
        var resp = await _client.PostFormAsync("/tasks/create", new[]
        {
            new KeyValuePair<string, string?>("Name",       "New Task"),
            new KeyValuePair<string, string?>("Priority",   "3"),
            new KeyValuePair<string, string?>("Status",     "ToDo"),
            new KeyValuePair<string, string?>("ProjectId",  projectId.ToString()),
            new KeyValuePair<string, string?>("AuthorId",   pmId.ToString()),
            new KeyValuePair<string, string?>("AssigneeId", pmId.ToString()),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.StartsWith("/tasks/", resp.Headers.Location!.ToString());
    }

    // ── PM authorization ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_AsOwningPm_Succeeds()
    {
        var pmId = await factory.SeedUserAsync("PM", "Own", "pm@local", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId);

        _client.AsProjectManager(pmId);
        var token = await _client.FetchTokenAsync($"/tasks/create?projectId={projectId}", Ct);
        var resp = await _client.PostFormAsync("/tasks/create", new[]
        {
            new KeyValuePair<string, string?>("Name",       "PM Task"),
            new KeyValuePair<string, string?>("Priority",   "1"),
            new KeyValuePair<string, string?>("Status",     "ToDo"),
            new KeyValuePair<string, string?>("ProjectId",  projectId.ToString()),
            new KeyValuePair<string, string?>("AuthorId",   pmId.ToString()),
            new KeyValuePair<string, string?>("AssigneeId", pmId.ToString()),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
    }

    [Fact]
    public async Task CreatePost_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectA = await CreateProjectAsync(pmA, "A's project");
        var projectB = await CreateProjectAsync(pmB, "B's project");

        _client.AsProjectManager(pmA);
        var token = await _client.FetchTokenAsync($"/tasks/create?projectId={projectA}", Ct);
        var resp = await _client.PostFormAsync("/tasks/create", new[]
        {
            new KeyValuePair<string, string?>("Name",       "Sneaky"),
            new KeyValuePair<string, string?>("Priority",   "1"),
            new KeyValuePair<string, string?>("Status",     "ToDo"),
            new KeyValuePair<string, string?>("ProjectId",  projectB.ToString()),
            new KeyValuePair<string, string?>("AuthorId",   pmB.ToString()),
            new KeyValuePair<string, string?>("AssigneeId", pmB.ToString()),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Edit_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectB = await CreateProjectAsync(pmB);
        var taskId = await CreateTaskAsync(projectB, pmB, pmB);

        _client.AsProjectManager(pmA);
        var resp = await _client.GetAsync($"/tasks/{taskId}/edit", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectA = await CreateProjectAsync(pmA, "A's project");
        var projectB = await CreateProjectAsync(pmB, "B's project");
        var taskA = await CreateTaskAsync(projectA, pmA, pmA);
        var taskB = await CreateTaskAsync(projectB, pmB, pmB);

        _client.AsProjectManager(pmA);
        var token = await _client.FetchTokenAsync($"/tasks/{taskA}", Ct);
        var resp = await _client.PostFormAsync($"/tasks/{taskB}/delete", Array.Empty<KeyValuePair<string, string?>>(), token, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectA = await CreateProjectAsync(pmA, "A's project");
        var projectB = await CreateProjectAsync(pmB, "B's project");
        var taskA = await CreateTaskAsync(projectA, pmA, pmA);
        var taskB = await CreateTaskAsync(projectB, pmB, pmB);

        _client.AsProjectManager(pmA);
        var token = await _client.FetchTokenAsync($"/tasks/{taskA}", Ct);
        var resp = await _client.PostFormAsync($"/tasks/{taskB}/status", new[]
        {
            new KeyValuePair<string, string?>("status", "Done"),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Employee authorization ───────────────────────────────────────────────

    [Fact]
    public async Task CreateGet_AsEmployee_Returns403()
    {
        var empId = await factory.SeedUserAsync("Emp", "W", "emp@local", Roles.Employee, Ct);

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync("/tasks/create", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task EditGet_AsEmployee_Returns403()
    {
        var pmId = await CreateEmployeeAsync("PM", "pm@test.com");
        var empId = await factory.SeedUserAsync("Emp", "W", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId });
        var taskId = await CreateTaskAsync(projectId, pmId, empId);

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync($"/tasks/{taskId}/edit", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Detail_AsNonParticipantEmployee_Returns403()
    {
        var pmId = await CreateEmployeeAsync("PM", "pm@test.com");
        var empId = await factory.SeedUserAsync("Emp", "Out", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId);
        var taskId = await CreateTaskAsync(projectId, pmId, pmId);

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync($"/tasks/{taskId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Detail_AsParticipant_Returns200()
    {
        var pmId = await CreateEmployeeAsync("PM", "pm@test.com");
        var empId = await factory.SeedUserAsync("Emp", "In", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId });
        var taskId = await CreateTaskAsync(projectId, pmId, empId);

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync($"/tasks/{taskId}", Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AsAssignee_RedirectsToDetail()
    {
        var pmId = await CreateEmployeeAsync("PM", "pm@test.com");
        var empId = await factory.SeedUserAsync("Emp", "W", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId });
        var taskId = await CreateTaskAsync(projectId, pmId, empId);

        _client.AsEmployee(empId);
        var token = await _client.FetchTokenAsync($"/tasks/{taskId}", Ct);
        var resp = await _client.PostFormAsync($"/tasks/{taskId}/status", new[]
        {
            new KeyValuePair<string, string?>("status", "InProgress"),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AsNonAssigneeEmployee_Returns403()
    {
        var pmId = await CreateEmployeeAsync("PM", "pm@test.com");
        var empA = await factory.SeedUserAsync("Emp", "A", "empA@local", Roles.Employee, Ct);
        var empB = await factory.SeedUserAsync("Emp", "B", "empB@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empA, empB });
        var taskA = await CreateTaskAsync(projectId, pmId, empA, "A's task");

        _client.AsEmployee(empB);
        var token = await _client.FetchTokenAsync($"/tasks/{taskA}", Ct);
        var resp = await _client.PostFormAsync($"/tasks/{taskA}/status", new[]
        {
            new KeyValuePair<string, string?>("status", "Done"),
        }, token, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── anonymous ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_Anonymous_Returns401()
    {
        _client.AsAnonymous();
        var resp = await _client.GetAsync("/tasks", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
