using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessLogic.Common;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Identity;
using BusinessLogic.Projects.Commands;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using Tests.Presentation.E2E;

namespace Tests.Presentation.Controllers;

public class TasksApiTests(ApiFactory factory)
    : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd   = new(2024, 12, 31);

    private record ApiProblem(string? Title, string? Detail);
    private record TaskPage(ProjectTaskDto[] Items, int TotalCount, int Page, int PageSize);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();
    public ValueTask DisposeAsync()          => ValueTask.CompletedTask;

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<int> CreateEmployeeAsync(
        string email = "pm@example.com",
        string role  = Roles.ProjectManager)
    {
        _client.AsDirector();
        var r = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ivan", lastName = "Petrov",
            patronymic = "Sergeevich", email,
            password = "Test#12345", role
        }, Ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateEmployeeResponse>(Json, Ct))!.Id;
    }

    private async Task<int> CreateProjectAsync(
        int pmId,
        string name = "Test Project",
        IEnumerable<int>? employeeIds = null)
    {
        _client.AsDirector();
        var r = await _client.PostAsJsonAsync("/api/projects", new
        {
            name,
            customerCompany  = "Acme Corp",
            executingCompany = "Dev Co",
            projectManagerId = pmId,
            priority         = 3,
            startDate        = DefaultStart,
            endDate          = DefaultEnd,
            employeeIds      = (employeeIds ?? Array.Empty<int>()).ToArray()
        }, Ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateProjectResponse>(Json, Ct))!.Id;
    }

    private async Task<int> CreateTaskAsync(
        int projectId, int authorId, int assigneeId,
        string name = "Test Task")
    {
        _client.AsDirector();
        var r = await _client.PostAsJsonAsync("/api/tasks", new
        {
            name, projectId, authorId, assigneeId,
            priority = 1, status = "ToDo"
        }, Ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateProjectTaskResponse>(Json, Ct))!.Id;
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTask_AsDirector_Returns201()
    {
        var pmId = await CreateEmployeeAsync("pm@test.com");
        var projectId = await CreateProjectAsync(pmId);

        _client.AsDirector();
        var r = await _client.PostAsJsonAsync("/api/tasks", new
        {
            name = "New Task", projectId, authorId = pmId, assigneeId = pmId,
            priority = 1, status = "ToDo"
        }, Ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_AfterCreation_ReturnsCorrectData()
    {
        var pmId = await CreateEmployeeAsync("pm@test.com");
        var projectId = await CreateProjectAsync(pmId);
        var taskId = await CreateTaskAsync(projectId, pmId, pmId, "Alpha Task");

        _client.AsDirector();
        var task = await (await _client.GetAsync($"/api/tasks/{taskId}", Ct))
            .Content.ReadFromJsonAsync<ProjectTaskDto>(Json, Ct);

        Assert.Equal("Alpha Task", task!.Name);
        Assert.Equal(projectId, task.ProjectId);
    }

    [Fact]
    public async Task EditTask_AsDirector_Returns200()
    {
        var pmId = await CreateEmployeeAsync("pm@test.com");
        var projectId = await CreateProjectAsync(pmId);
        var taskId = await CreateTaskAsync(projectId, pmId, pmId);

        _client.AsDirector();
        var r = await _client.PutAsJsonAsync($"/api/tasks/{taskId}", new
        {
            name = "Renamed Task"
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_AsDirector_SubsequentGetReturns404()
    {
        var pmId = await CreateEmployeeAsync("pm@test.com");
        var projectId = await CreateProjectAsync(pmId);
        var taskId = await CreateTaskAsync(projectId, pmId, pmId);

        _client.AsDirector();
        var delResp = await _client.DeleteAsync($"/api/tasks/{taskId}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        var getResp = await _client.GetAsync($"/api/tasks/{taskId}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AsDirector_Returns204()
    {
        var pmId = await CreateEmployeeAsync("pm@test.com");
        var projectId = await CreateProjectAsync(pmId);
        var taskId = await CreateTaskAsync(projectId, pmId, pmId);

        _client.AsDirector();
        var r = await _client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new
        {
            status = "InProgress"
        }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
    }

    // ── PM authorization ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTask_AsOwningPm_Returns201()
    {
        var pmId = await factory.SeedUserAsync("PM", "Own", "pm@local", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId);

        _client.AsProjectManager(pmId);
        var r = await _client.PostAsJsonAsync("/api/tasks", new
        {
            name = "PM Task", projectId, authorId = pmId, assigneeId = pmId,
            priority = 1, status = "ToDo"
        }, Ct);

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task CreateTask_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectB = await CreateProjectAsync(pmB);

        _client.AsProjectManager(pmA);
        var r = await _client.PostAsJsonAsync("/api/tasks", new
        {
            name = "Sneaky", projectId = projectB, authorId = pmB, assigneeId = pmB,
            priority = 1, status = "ToDo"
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task GetTasks_AsPm_OnlyReturnsTasksFromTheirProjects()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectA = await CreateProjectAsync(pmA, "Project A");
        var projectB = await CreateProjectAsync(pmB, "Project B");
        await CreateTaskAsync(projectA, pmA, pmA, "Task A");
        await CreateTaskAsync(projectB, pmB, pmB, "Task B");

        _client.AsProjectManager(pmA);
        var result = await (await _client.GetAsync("/api/tasks", Ct))
            .Content.ReadFromJsonAsync<TaskPage>(Json, Ct);

        Assert.Single(result!.Items);
        Assert.Equal("Task A", result.Items[0].Name);
    }

    [Fact]
    public async Task GetTaskById_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectB = await CreateProjectAsync(pmB);
        var taskId = await CreateTaskAsync(projectB, pmB, pmB);

        _client.AsProjectManager(pmA);
        var r = await _client.GetAsync($"/api/tasks/{taskId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task EditTask_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectB = await CreateProjectAsync(pmB);
        var taskId = await CreateTaskAsync(projectB, pmB, pmB);

        _client.AsProjectManager(pmA);
        var r = await _client.PutAsJsonAsync($"/api/tasks/{taskId}", new { name = "Hijacked" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectB = await CreateProjectAsync(pmB);
        var taskId = await CreateTaskAsync(projectB, pmB, pmB);

        _client.AsProjectManager(pmA);
        var r = await _client.DeleteAsync($"/api/tasks/{taskId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AsOwningPm_Returns204()
    {
        var pmId = await factory.SeedUserAsync("PM", "Own", "pm@local", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId);
        var taskId = await CreateTaskAsync(projectId, pmId, pmId);

        _client.AsProjectManager(pmId);
        var r = await _client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new
        {
            status = "Done"
        }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AsPmOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var projectB = await CreateProjectAsync(pmB);
        var taskId = await CreateTaskAsync(projectB, pmB, pmB);

        _client.AsProjectManager(pmA);
        var r = await _client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new
        {
            status = "Done"
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ── Employee authorization ───────────────────────────────────────────────

    [Fact]
    public async Task CreateTask_AsEmployee_Returns403()
    {
        var pmId = await CreateEmployeeAsync("pm@test.com");
        var empId = await factory.SeedUserAsync("Emp", "W", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId });

        _client.AsEmployee(empId);
        var r = await _client.PostAsJsonAsync("/api/tasks", new
        {
            name = "Sneaky", projectId, authorId = empId, assigneeId = empId,
            priority = 1, status = "ToDo"
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task GetTasks_AsEmployee_OnlyReturnsTasksFromTheirProjects()
    {
        var pmId = await factory.SeedUserAsync("PM", "P", "pm@local", Roles.ProjectManager, Ct);
        var empId = await factory.SeedUserAsync("Emp", "W", "emp@local", Roles.Employee, Ct);
        var projectIn  = await CreateProjectAsync(pmId, "Project In", employeeIds: new[] { empId });
        var projectOut = await CreateProjectAsync(pmId, "Project Out");
        await CreateTaskAsync(projectIn, pmId, empId, "My Task");
        await CreateTaskAsync(projectOut, pmId, pmId, "Not My Task");

        _client.AsEmployee(empId);
        var result = await (await _client.GetAsync("/api/tasks", Ct))
            .Content.ReadFromJsonAsync<TaskPage>(Json, Ct);

        Assert.Single(result!.Items);
        Assert.Equal("My Task", result.Items[0].Name);
    }

    [Fact]
    public async Task GetTaskById_AsNonParticipantEmployee_Returns403()
    {
        var pmId = await factory.SeedUserAsync("PM", "P", "pm@local", Roles.ProjectManager, Ct);
        var empId = await factory.SeedUserAsync("Emp", "O", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId);
        var taskId = await CreateTaskAsync(projectId, pmId, pmId);

        _client.AsEmployee(empId);
        var r = await _client.GetAsync($"/api/tasks/{taskId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task EditTask_AsEmployee_Returns403()
    {
        var pmId = await factory.SeedUserAsync("PM", "P", "pm@local", Roles.ProjectManager, Ct);
        var empId = await factory.SeedUserAsync("Emp", "W", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId });
        var taskId = await CreateTaskAsync(projectId, pmId, empId);

        _client.AsEmployee(empId);
        var r = await _client.PutAsJsonAsync($"/api/tasks/{taskId}", new { name = "Hijacked" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_AsEmployee_Returns403()
    {
        var pmId = await factory.SeedUserAsync("PM", "P", "pm@local", Roles.ProjectManager, Ct);
        var empId = await factory.SeedUserAsync("Emp", "W", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId });
        var taskId = await CreateTaskAsync(projectId, pmId, empId);

        _client.AsEmployee(empId);
        var r = await _client.DeleteAsync($"/api/tasks/{taskId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AsAssignee_Returns204()
    {
        var pmId = await factory.SeedUserAsync("PM", "P", "pm@local", Roles.ProjectManager, Ct);
        var empId = await factory.SeedUserAsync("Emp", "W", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId });
        var taskId = await CreateTaskAsync(projectId, pmId, empId);

        _client.AsEmployee(empId);
        var r = await _client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new
        {
            status = "InProgress"
        }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AsNonAssigneeEmployee_Returns403()
    {
        var pmId = await factory.SeedUserAsync("PM", "P", "pm@local", Roles.ProjectManager, Ct);
        var empA = await factory.SeedUserAsync("Emp", "A", "empA@local", Roles.Employee, Ct);
        var empB = await factory.SeedUserAsync("Emp", "B", "empB@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empA, empB });
        var taskId = await CreateTaskAsync(projectId, pmId, empA, "A's Task");

        _client.AsEmployee(empB);
        var r = await _client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new
        {
            status = "Done"
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ── anonymous ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTasks_Anonymous_Returns401()
    {
        _client.AsAnonymous();
        var r = await _client.GetAsync("/api/tasks", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}
