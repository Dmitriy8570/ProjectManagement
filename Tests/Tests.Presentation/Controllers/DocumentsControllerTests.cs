using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessLogic.Documents;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Identity;
using BusinessLogic.Projects.Commands;
using Tests.Presentation.E2E;

namespace Tests.Presentation.Controllers;

/// <summary>
/// E2E tests for the /api/projects/{id}/documents and /api/documents/{id}
/// endpoints. Boots the real API pipeline against in-memory SQLite and a
/// temp file-storage directory (configured by ApiFactory), so each test
/// exercises the full upload → list → download → delete cycle. Adds
/// role-based coverage: only Director/PM may upload/delete.
/// </summary>
public class DocumentsApiTests(ApiFactory factory)
    : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private record ApiProblem(string? Title, string? Detail);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();
    public ValueTask DisposeAsync()          => ValueTask.CompletedTask;

    // ── helpers ──────────────────────────────────────────────────────────────

    // Creates a project owned by a freshly seeded PM and returns both ids — the
    // role-restricted tests need the PM's employee id to claim ownership.
    private async Task<(int ProjectId, int PmEmployeeId)> CreateProjectAsync(
        string pmEmail = "pm@example.com")
    {
        // Director creates the PM account + project so the seed is consistent.
        _client.AsDirector();

        var emailUnique = $"{Guid.NewGuid():N}-{pmEmail}";

        var pm = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ivan", lastName = "Petrov",
            patronymic = "Sergeevich", email = emailUnique,
            password = "Test#12345", role = Roles.ProjectManager
        }, Ct);
        pm.EnsureSuccessStatusCode();
        var pmId = (await pm.Content.ReadFromJsonAsync<CreateEmployeeResponse>(Json, Ct))!.Id;

        var resp = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Project", customerCompany = "C", executingCompany = "E",
            startDate = new DateTime(2026, 1, 1),
            endDate   = new DateTime(2026, 12, 31),
            projectManagerId = pmId, employeeIds = Array.Empty<int>(), priority = 1
        }, Ct);
        resp.EnsureSuccessStatusCode();
        var projectId = (await resp.Content.ReadFromJsonAsync<CreateProjectResponse>(Json, Ct))!.Id;
        return (projectId, pmId);
    }

    private static MultipartFormDataContent FilePart(
        byte[] bytes, string fileName, string contentType = "text/plain")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadListDownload_RoundTripsFileBytes()
    {
        var (projectId, _) = await CreateProjectAsync();
        var bytes = Encoding.UTF8.GetBytes("payload");

        _client.AsDirector();
        using var form = FilePart(bytes, "notes.txt");
        var upload = await _client.PostAsync($"/api/projects/{projectId}/documents", form, Ct);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        var dto = await upload.Content.ReadFromJsonAsync<ProjectDocumentDto>(Json, Ct);
        Assert.NotNull(dto);
        Assert.Equal("notes.txt", dto!.FileName);
        Assert.Equal(projectId,   dto.ProjectId);

        var list = await (await _client.GetAsync(
            $"/api/projects/{projectId}/documents", Ct))
            .Content.ReadFromJsonAsync<ProjectDocumentDto[]>(Json, Ct);
        Assert.Single(list!);
        Assert.Equal(dto.Id, list![0].Id);

        var download = await _client.GetAsync($"/api/documents/{dto.Id}/download", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task UploadDocument_MissingFile_Returns400()
    {
        var (projectId, _) = await CreateProjectAsync();

        _client.AsDirector();
        using var form = new MultipartFormDataContent();
        var resp = await _client.PostAsync($"/api/projects/{projectId}/documents", form, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteDocument_RemovesFromListAndDownload()
    {
        var (projectId, _) = await CreateProjectAsync();

        _client.AsDirector();
        using var form = FilePart(Encoding.UTF8.GetBytes("data"), "x.txt");
        var upload = await _client.PostAsync($"/api/projects/{projectId}/documents", form, Ct);
        var dto = await upload.Content.ReadFromJsonAsync<ProjectDocumentDto>(Json, Ct);

        var del = await _client.DeleteAsync($"/api/documents/{dto!.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var list = await (await _client.GetAsync(
            $"/api/projects/{projectId}/documents", Ct))
            .Content.ReadFromJsonAsync<ProjectDocumentDto[]>(Json, Ct);
        Assert.Empty(list!);

        var download = await _client.GetAsync($"/api/documents/{dto.Id}/download", Ct);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task DownloadDocument_NonExistent_Returns404WithResourceNotFoundProblem()
    {
        _client.AsDirector();
        var resp = await _client.GetAsync("/api/documents/99999/download", Ct);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var problem = await resp.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);
        Assert.Equal("Resource not found", problem!.Title);
    }

    [Fact]
    public async Task DeleteDocument_NonExistent_Returns404WithResourceNotFoundProblem()
    {
        _client.AsDirector();
        var resp = await _client.DeleteAsync("/api/documents/99999", Ct);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var problem = await resp.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);
        Assert.Equal("Resource not found", problem!.Title);
    }

    [Fact]
    public async Task GetDocuments_NoUploads_ReturnsEmptyArray()
    {
        var (projectId, _) = await CreateProjectAsync();

        _client.AsDirector();
        var list = await (await _client.GetAsync(
            $"/api/projects/{projectId}/documents", Ct))
            .Content.ReadFromJsonAsync<ProjectDocumentDto[]>(Json, Ct);

        Assert.Empty(list!);
    }

    // ── Role-based authorization ─────────────────────────────────────────────

    // Owning PM uploads to their own project — happy path under a non-Director.
    [Fact]
    public async Task UploadDocument_AsOwningProjectManager_Returns201()
    {
        var (projectId, pmId) = await CreateProjectAsync();

        _client.AsProjectManager(pmId);
        using var form = FilePart(Encoding.UTF8.GetBytes("data"), "x.txt");
        var upload = await _client.PostAsync($"/api/projects/{projectId}/documents", form, Ct);

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
    }

    // A PM cannot upload onto someone else's project — CanManageAsync gates this.
    [Fact]
    public async Task UploadDocument_AsProjectManagerOfDifferentProject_Returns403()
    {
        var (projectId, _) = await CreateProjectAsync();
        var outsiderId = await factory.SeedUserAsync("Other", "PM", "other@local", Roles.ProjectManager, Ct);

        _client.AsProjectManager(outsiderId);
        using var form = FilePart(Encoding.UTF8.GetBytes("data"), "x.txt");
        var upload = await _client.PostAsync($"/api/projects/{projectId}/documents", form, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
    }

    // Plain Сотрудник may never upload — even to a project they participate in.
    [Fact]
    public async Task UploadDocument_AsEmployee_Returns403()
    {
        var (projectId, _) = await CreateProjectAsync();
        var empId = await factory.SeedUserAsync("Emp", "Worker", "emp@local", Roles.Employee, Ct);

        _client.AsEmployee(empId);
        using var form = FilePart(Encoding.UTF8.GetBytes("data"), "x.txt");
        var upload = await _client.PostAsync($"/api/projects/{projectId}/documents", form, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
    }

    // List access mirrors Detail visibility: a participant Сотрудник may read
    // the document list on their own project.
    [Fact]
    public async Task GetDocuments_AsParticipantEmployee_Returns200()
    {
        var (projectId, pmId) = await CreateProjectAsync();
        var empId = await factory.SeedUserAsync("Emp", "Member", "emp@local", Roles.Employee, Ct);

        _client.AsDirector();
        await _client.PatchAsJsonAsync("/api/projects/assign",
            new { projectId, employeeId = empId }, Ct);

        _client.AsEmployee(empId);
        var resp = await _client.GetAsync($"/api/projects/{projectId}/documents", Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // …but never on a project they don't belong to.
    [Fact]
    public async Task GetDocuments_AsNonParticipantEmployee_Returns403()
    {
        var (projectId, _) = await CreateProjectAsync();
        var outsiderId = await factory.SeedUserAsync("Out", "Sider", "out@local", Roles.Employee, Ct);

        _client.AsEmployee(outsiderId);
        var resp = await _client.GetAsync($"/api/projects/{projectId}/documents", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
