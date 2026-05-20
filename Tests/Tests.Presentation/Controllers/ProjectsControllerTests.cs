using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using Tests.Presentation.E2E;

namespace Tests.Presentation.Controllers;

/// <summary>
/// E2E tests for the /api/projects and related /api/employees endpoints.
/// The factory boots the real ASP.NET Core pipeline against SQLite in-memory,
/// so every test exercises the full stack from HTTP to the database.
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
        CancellationToken ct = default)
    {
        var r = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ivan", lastName = "Petrov",
            patronymic = "Sergeevich", email
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
        CancellationToken ct = default)
    {
        var r = await _client.PostAsJsonAsync("/api/projects", new
        {
            name,
            customerCompany  = "Acme Corp",
            executingCompany = "Dev Co",
            projectManagerId = pmId,
            priority,
            startDate   = start ?? DefaultStart,
            endDate     = end   ?? DefaultEnd,
            employeeIds = Array.Empty<int>()
        }, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateProjectResponse>(Json, ct))!.Id;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // The GET /api/projects/{id} response must include the project manager's
    // data eagerly loaded by the repository — this verifies the EF Include
    // and the DTO projection in one shot.
    [Fact]
    public async Task GetProject_AfterCreation_IncludesPmDataAndEmptyTeam()
    {
        var pmId      = await CreateEmployeeAsync("pm@example.com", Ct);
        var projectId = await CreateProjectAsync(pmId, "Alpha Project", ct: Ct);

        var project = await (await _client.GetAsync($"/api/projects/{projectId}", Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);

        Assert.Equal("Alpha Project", project!.Name);
        Assert.Equal(pmId, project.ProjectManager.Id);
        Assert.Empty(project.Employees);
    }

    // DomainGuard.DateRange throws DomainValidationException which must be
    // mapped to a 400 "Validation failed" response by DomainExceptionHandler.
    [Fact]
    public async Task CreateProject_EndDateBeforeStartDate_Returns400WithValidationProblem()
    {
        var pmId = await CreateEmployeeAsync("pm2@example.com", Ct);

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

    // The handler throws EntityNotFoundException when the PM does not exist.
    // DomainExceptionHandler must translate that to a 404 "Resource not found".
    [Fact]
    public async Task CreateProject_NonExistentProjectManager_Returns404WithResourceNotFoundProblem()
    {
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

    // Verifies the full assign → verify → unassign → verify cycle through the
    // HTTP layer so we know the EF-tracked many-to-many relation is actually
    // persisted and re-read correctly between requests.
    [Fact]
    public async Task AssignAndUnassignEmployee_TeamMembershipReflectedOnGet()
    {
        var pmId      = await CreateEmployeeAsync("pm3@example.com", Ct);
        var memberId  = await CreateEmployeeAsync("member@example.com", Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

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

    // Project.AddEmployee enforces the invariant that the PM cannot also be a
    // regular participant — this must surface as a 400 domain problem, not a 500.
    [Fact]
    public async Task AssignProjectManagerAsParticipant_Returns400WithValidationProblem()
    {
        var pmId      = await CreateEmployeeAsync("pm4@example.com", Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

        var response = await _client.PatchAsJsonAsync("/api/projects/assign",
            new { projectId, employeeId = pmId }, Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", problem!.Title);
    }

    // The MinPriority filter is applied in the repository query — verifies that
    // the filter reaches the database and only matching rows come back.
    [Fact]
    public async Task GetProjects_FilterByMinPriority_ReturnsOnlyMatchingProjects()
    {
        var pmId = await CreateEmployeeAsync("pm5@example.com", Ct);
        await CreateProjectAsync(pmId, "Low",    priority: 2, ct: Ct);
        await CreateProjectAsync(pmId, "Medium", priority: 5, ct: Ct);
        await CreateProjectAsync(pmId, "High",   priority: 8, ct: Ct);

        var result = await (await _client.GetAsync("/api/projects?minPriority=5", Ct))
            .Content.ReadFromJsonAsync<ProjectPage>(Json, Ct);

        Assert.Equal(2, result!.TotalCount);
        Assert.All(result.Items, p => Assert.True(p.Priority >= 5));
    }

    // Verifies that page/size clamping and the skip/take logic returns the
    // correct window of items and the correct unfiltered total count.
    [Fact]
    public async Task GetProjects_Pagination_ReturnsCorrectWindowAndTotalCount()
    {
        var pmId = await CreateEmployeeAsync("pm6@example.com", Ct);
        for (var i = 1; i <= 5; i++)
            await CreateProjectAsync(pmId, $"Project {i}", ct: Ct);

        var result = await (await _client.GetAsync("/api/projects?page=2&pageSize=2", Ct))
            .Content.ReadFromJsonAsync<ProjectPage>(Json, Ct);

        Assert.Equal(5, result!.TotalCount);
        Assert.Equal(2, result.Items.Length);
        Assert.Equal(2, result.Page);
    }

    // Deletion must actually remove the record — not just return 204 but also
    // make subsequent reads return 404 with the expected problem title.
    [Fact]
    public async Task DeleteProject_SubsequentGetReturns404WithResourceNotFoundProblem()
    {
        var pmId      = await CreateEmployeeAsync("pm7@example.com", Ct);
        var projectId = await CreateProjectAsync(pmId, ct: Ct);

        await _client.DeleteAsync($"/api/projects/{projectId}", Ct);
        var response = await _client.GetAsync($"/api/projects/{projectId}", Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Resource not found", problem!.Title);
    }

    // The DeleteEmployeeCommandHandler pre-checks IsProjectManagerAsync so the
    // FK RESTRICT never fires — instead we get a clean 400 domain problem.
    [Fact]
    public async Task DeleteEmployee_WhoIsActiveProjectManager_Returns400WithValidationProblem()
    {
        var pmId = await CreateEmployeeAsync("pm8@example.com", Ct);
        await CreateProjectAsync(pmId, ct: Ct);

        var response = await _client.DeleteAsync($"/api/employees/{pmId}", Ct);
        var problem  = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation failed", problem!.Title);
    }
}
