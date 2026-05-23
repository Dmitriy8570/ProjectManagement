using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Identity;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using Tests.Presentation.E2E;

namespace Tests.Presentation.Controllers;

/// <summary>
/// E2E tests for /api/projects. Covers the happy path as Director and proves
/// the role rules from the spec — ProjectManager only sees/edits their own
/// projects, Сотрудник only sees ones they participate in.
/// </summary>
public class ProjectsApiTests(ApiFactory factory)
    : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly DateTime DefaultStart = new(2024,  1,  1);
    private static readonly DateTime DefaultEnd   = new(2024, 12, 31);

    private record ApiProblem(string? Title, string? Detail);

    // Inline projection because PagedResult<T>.Items is IReadOnlyList<T> and
    // STJ 10 will not always materialise an interface type into an indexable
    // collection — a concrete array avoids the ambiguity.
    private record ProjectPage(ProjectDto[] Items, int TotalCount, int Page, int PageSize);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();
    public ValueTask DisposeAsync()          => ValueTask.CompletedTask;

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<int> CreateEmployeeAsync(
        string email = "pm@example.com",
        string role  = Roles.ProjectManager,
        CancellationToken ct = default)
    {
        _client.AsDirector();
        var r = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ivan", lastName = "Petrov",
            patronymic = "Sergeevich", email,
            password = "Test#12345", role
        }, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateEmployeeResponse>(Json, ct))!.Id;
    }

    private async Task<int> CreateProjectAsync(
        int    pmId,
        string name     = "Test Project",
        int    priority = 3,
        DateTime? start = null,
        DateTime? end   = null,
        IEnumerable<int>? employeeIds = null,
        CancellationToken ct = default)
    {
        _client.AsDirector();
        var r = await _client.PostAsJsonAsync("/api/projects", new
        {
            name,
            customerCompany  = "Acme Corp",
            executingCompany = "Dev Co",
            projectManagerId = pmId,
            priority,
            startDate   = start ?? DefaultStart,
            endDate     = end   ?? DefaultEnd,
            employeeIds = (employeeIds ?? Array.Empty<int>()).ToArray()
        }, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateProjectResponse>(Json, ct))!.Id;
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProject_AfterCreation_IncludesPmDataAndEmptyTeam()
    {
        var pmId      = await CreateEmployeeAsync("pm@example.com", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId, "Alpha Project", ct: Ct);

        _client.AsDirector();
        var project = await (await _client.GetAsync($"/api/projects/{projectId}", Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);

        Assert.Equal("Alpha Project", project!.Name);
        Assert.Equal(pmId, project.ProjectManager.Id);
        Assert.Empty(project.Employees);
    }

    [Fact]
    public async Task CreateProject_EndDateBeforeStartDate_Returns400WithValidationProblem()
    {
        var pmId = await CreateEmployeeAsync("pm2@example.com", Roles.ProjectManager, Ct);

        _client.AsDirector();
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Bad Dates", customerCompany = "Acme", executingCompany = "Dev",
            projectManagerId = pmId, priority = 1,
            startDate   = new DateTime(2024, 12, 31),
            endDate     = new DateTime(2024,  1,  1),
            employeeIds = Array.Empty<int>()
        }, Ct);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", problem!.Title);
    }

    [Fact]
    public async Task CreateProject_NonExistentProjectManager_Returns404WithResourceNotFoundProblem()
    {
        _client.AsDirector();
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Ghost PM", customerCompany = "Acme", executingCompany = "Dev",
            projectManagerId = 99999, priority = 1,
            startDate   = DefaultStart, endDate = DefaultEnd,
            employeeIds = Array.Empty<int>()
        }, Ct);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found", problem!.Title);
    }

    // CreateProjectCommandHandler refuses to appoint a plain Сотрудник as PM,
    // even when the HTTP caller is the all-powerful Director.
    [Fact]
    public async Task CreateProject_PmIsPlainEmployee_Returns400WithValidationProblem()
    {
        var empId = await CreateEmployeeAsync("emp@example.com", Roles.Employee, Ct);

        _client.AsDirector();
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "X", customerCompany = "C", executingCompany = "E",
            projectManagerId = empId, priority = 1,
            startDate = DefaultStart, endDate = DefaultEnd,
            employeeIds = Array.Empty<int>()
        }, Ct);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", problem!.Title);
    }

    [Fact]
    public async Task AssignAndUnassignEmployee_TeamMembershipReflectedOnGet()
    {
        var pmId      = await CreateEmployeeAsync("pm3@example.com", Roles.ProjectManager, Ct);
        var memberId  = await CreateEmployeeAsync("member@example.com", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

        _client.AsDirector();
        var assignStatus = (await _client.PatchAsJsonAsync("/api/projects/assign",
            new { projectId, employeeId = memberId }, Ct)).StatusCode;
        Assert.Equal(HttpStatusCode.NoContent, assignStatus);

        var afterAssign = await (await _client.GetAsync($"/api/projects/{projectId}", Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);
        Assert.Single(afterAssign!.Employees);
        Assert.Equal(memberId, afterAssign.Employees[0].Id);

        await _client.PatchAsJsonAsync("/api/projects/unassign",
            new { projectId, employeeId = memberId }, Ct);

        var afterUnassign = await (await _client.GetAsync($"/api/projects/{projectId}", Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);
        Assert.Empty(afterUnassign!.Employees);
    }

    [Fact]
    public async Task AssignProjectManagerAsParticipant_Returns400WithValidationProblem()
    {
        var pmId      = await CreateEmployeeAsync("pm4@example.com", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

        _client.AsDirector();
        var response = await _client.PatchAsJsonAsync("/api/projects/assign",
            new { projectId, employeeId = pmId }, Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", problem!.Title);
    }

    [Fact]
    public async Task GetProjects_FilterByMinPriority_ReturnsOnlyMatchingProjects()
    {
        var pmId = await CreateEmployeeAsync("pm5@example.com", Roles.ProjectManager, Ct);
        await CreateProjectAsync(pmId, "Low",    priority: 2, ct: Ct);
        await CreateProjectAsync(pmId, "Medium", priority: 5, ct: Ct);
        await CreateProjectAsync(pmId, "High",   priority: 8, ct: Ct);

        _client.AsDirector();
        var result = await (await _client.GetAsync("/api/projects?minPriority=5", Ct))
            .Content.ReadFromJsonAsync<ProjectPage>(Json, Ct);

        Assert.Equal(2, result!.TotalCount);
        Assert.All(result.Items, p => Assert.True(p.Priority >= 5));
    }

    [Fact]
    public async Task GetProjects_Pagination_ReturnsCorrectWindowAndTotalCount()
    {
        var pmId = await CreateEmployeeAsync("pm6@example.com", Roles.ProjectManager, Ct);
        for (var i = 1; i <= 5; i++)
            await CreateProjectAsync(pmId, $"Project {i}", ct: Ct);

        _client.AsDirector();
        var result = await (await _client.GetAsync("/api/projects?page=2&pageSize=2", Ct))
            .Content.ReadFromJsonAsync<ProjectPage>(Json, Ct);

        Assert.Equal(5, result!.TotalCount);
        Assert.Equal(2, result.Items.Length);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task DeleteProject_SubsequentGetReturns404WithResourceNotFoundProblem()
    {
        var pmId      = await CreateEmployeeAsync("pm7@example.com", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

        _client.AsDirector();
        await _client.DeleteAsync($"/api/projects/{projectId}", Ct);
        var response = await _client.GetAsync($"/api/projects/{projectId}", Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found", problem!.Title);
    }

    [Fact]
    public async Task DeleteEmployee_WhoIsActiveProjectManager_Returns400WithValidationProblem()
    {
        var pmId = await CreateEmployeeAsync("pm8@example.com", Roles.ProjectManager, Ct);
        await CreateProjectAsync(pmId, ct: Ct);

        _client.AsDirector();
        var response = await _client.DeleteAsync($"/api/employees/{pmId}", Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", problem!.Title);
    }

    // ── Role-based authorization ─────────────────────────────────────────────

    // ProjectManager only sees and edits their own projects. Index filters at
    // the query layer so a PM cannot even SEE someone else's project.
    [Fact]
    public async Task GetProjects_AsProjectManager_OnlyReturnsTheirOwnProjects()
    {
        // Seed two PMs and one project for each — each PM should only see one.
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var pA = await CreateProjectAsync(pmA, "A's Project", ct: Ct);
        var pB = await CreateProjectAsync(pmB, "B's Project", ct: Ct);

        _client.AsProjectManager(pmA);
        var result = await (await _client.GetAsync("/api/projects", Ct))
            .Content.ReadFromJsonAsync<ProjectPage>(Json, Ct);

        Assert.Single(result!.Items);
        Assert.Equal(pA, result.Items[0].Id);
        Assert.DoesNotContain(result.Items, p => p.Id == pB);
    }

    // Employee sees only the projects they participate in — never their PM's
    // unrelated projects.
    [Fact]
    public async Task GetProjects_AsEmployee_OnlyReturnsParticipantProjects()
    {
        var pmId = await factory.SeedUserAsync("PM", "Owner", "pm@local", Roles.ProjectManager, Ct);
        var empId = await factory.SeedUserAsync("Emp", "Worker", "emp@local", Roles.Employee, Ct);

        var participatingId = await CreateProjectAsync(pmId, "I belong here", employeeIds: new[] { empId }, ct: Ct);
        var outsiderId = await CreateProjectAsync(pmId, "I do not belong here", ct: Ct);

        _client.AsEmployee(empId);
        var result = await (await _client.GetAsync("/api/projects", Ct))
            .Content.ReadFromJsonAsync<ProjectPage>(Json, Ct);

        Assert.Single(result!.Items);
        Assert.Equal(participatingId, result.Items[0].Id);
        Assert.DoesNotContain(result.Items, p => p.Id == outsiderId);
    }

    // Reading a single project the user is unrelated to must return 403, not
    // 200 — direct URL access bypasses the Index filter so the resource check
    // has to catch it.
    [Fact]
    public async Task GetProjectById_AsProjectManagerOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var pB = await CreateProjectAsync(pmB, "B's Project", ct: Ct);

        _client.AsProjectManager(pmA);
        var response = await _client.GetAsync($"/api/projects/{pB}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Same rule for Сотрудник: visiting a project they don't participate in
    // is forbidden.
    [Fact]
    public async Task GetProjectById_AsNonParticipantEmployee_Returns403()
    {
        var pmId = await factory.SeedUserAsync("PM", "Owner", "pm@local", Roles.ProjectManager, Ct);
        var empId = await factory.SeedUserAsync("Emp", "Outsider", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

        _client.AsEmployee(empId);
        var response = await _client.GetAsync($"/api/projects/{projectId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Creating a project is Director-only.
    [Fact]
    public async Task CreateProject_AsProjectManager_Returns403()
    {
        var pmId = await factory.SeedUserAsync("PM", "P", "pm@local", Roles.ProjectManager, Ct);
        var someoneElse = await CreateEmployeeAsync("other@example.com", Roles.ProjectManager, Ct);

        _client.AsProjectManager(pmId);
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Sneaky", customerCompany = "C", executingCompany = "E",
            projectManagerId = someoneElse, priority = 1,
            startDate = DefaultStart, endDate = DefaultEnd,
            employeeIds = Array.Empty<int>()
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // PMs can edit only their own projects — Edit on someone else's returns 403.
    [Fact]
    public async Task EditProject_AsProjectManagerOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var pB = await CreateProjectAsync(pmB, "B's Project", ct: Ct);

        _client.AsProjectManager(pmA);
        var response = await _client.PutAsJsonAsync($"/api/projects/{pB}", new
        {
            name = "Hijacked"
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EditProject_AsOwningProjectManager_Returns200()
    {
        var pmId = await factory.SeedUserAsync("PM", "Own", "pm@local", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId, "Original", ct: Ct);

        _client.AsProjectManager(pmId);
        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name = "Renamed by PM"
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.AsDirector();
        var fetched = await (await _client.GetAsync($"/api/projects/{projectId}", Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);
        Assert.Equal("Renamed by PM", fetched!.Name);
    }

    // Сотрудник cannot edit a project even one they participate in — Edit is
    // PM+Director only.
    [Fact]
    public async Task EditProject_AsParticipantEmployee_Returns403()
    {
        var pmId = await factory.SeedUserAsync("PM", "Owner", "pm@local", Roles.ProjectManager, Ct);
        var empId = await factory.SeedUserAsync("Emp", "Member", "emp@local", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, employeeIds: new[] { empId }, ct: Ct);

        _client.AsEmployee(empId);
        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name = "Worker tried to rename"
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Only Director can delete projects.
    [Fact]
    public async Task DeleteProject_AsProjectManager_Returns403()
    {
        var pmId = await factory.SeedUserAsync("PM", "Own", "pm@local", Roles.ProjectManager, Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

        _client.AsProjectManager(pmId);
        var response = await _client.DeleteAsync($"/api/projects/{projectId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Owning PMs can assign team members on their own projects.
    [Fact]
    public async Task AssignEmployee_AsOwningProjectManager_Succeeds()
    {
        var pmId = await factory.SeedUserAsync("PM", "Own", "pm@local", Roles.ProjectManager, Ct);
        var memberId = await CreateEmployeeAsync("worker@example.com", Roles.Employee, Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

        _client.AsProjectManager(pmId);
        var assignResp = await _client.PatchAsJsonAsync("/api/projects/assign",
            new { projectId, employeeId = memberId }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, assignResp.StatusCode);
    }

    // …but not on someone else's project.
    [Fact]
    public async Task AssignEmployee_AsProjectManagerOfDifferentProject_Returns403()
    {
        var pmA = await factory.SeedUserAsync("PM", "A", "pma@local", Roles.ProjectManager, Ct);
        var pmB = await factory.SeedUserAsync("PM", "B", "pmb@local", Roles.ProjectManager, Ct);
        var memberId = await CreateEmployeeAsync("worker@example.com", Roles.Employee, Ct);
        var projectB = await CreateProjectAsync(pmB, ct: Ct);

        _client.AsProjectManager(pmA);
        var response = await _client.PatchAsJsonAsync("/api/projects/assign",
            new { projectId = projectB, employeeId = memberId }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
