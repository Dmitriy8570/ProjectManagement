using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessLogic.Documents;
using BusinessLogic.Employees.Commands;
using BusinessLogic.Projects.Commands;
using Tests.Presentation.E2E;

namespace Tests.Presentation.Controllers;

/// <summary>
/// E2E tests for the /api/projects/{id}/documents and /api/documents/{id}
/// endpoints. Boots the real API pipeline against in-memory SQLite and a
/// temp file-storage directory (configured by ApiFactory), so each test
/// exercises the full upload → list → download → delete cycle.
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

    private async Task<int> CreateProjectAsync()
    {
        var pm = await _client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ivan", lastName = "Petrov",
            patronymic = "Sergeevich", email = $"{Guid.NewGuid():N}@x.com"
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
        return (await resp.Content.ReadFromJsonAsync<CreateProjectResponse>(Json, Ct))!.Id;
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

    // Happy path: upload returns 201 with a DTO describing the new document,
    // GetDocuments lists it, GetDownload streams identical bytes back.
    [Fact]
    public async Task UploadListDownload_RoundTripsFileBytes()
    {
        var projectId = await CreateProjectAsync();
        var bytes = Encoding.UTF8.GetBytes("payload");

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

    // No file part on the upload must surface as a clean 400 with the
    // controller-supplied problem title — not a generic 500.
    [Fact]
    public async Task UploadDocument_MissingFile_Returns400()
    {
        var projectId = await CreateProjectAsync();

        using var form = new MultipartFormDataContent();
        var resp = await _client.PostAsync($"/api/projects/{projectId}/documents", form, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // Deleting a document removes both the list entry and the download
    // endpoint behind it — a follow-up GET should produce a 404 problem.
    [Fact]
    public async Task DeleteDocument_RemovesFromListAndDownload()
    {
        var projectId = await CreateProjectAsync();

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

    // Missing-id paths must surface through DomainExceptionHandler as the
    // typed "Resource not found" problem, not as raw exceptions.
    [Fact]
    public async Task DownloadDocument_NonExistent_Returns404WithResourceNotFoundProblem()
    {
        var resp = await _client.GetAsync("/api/documents/99999/download", Ct);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var problem = await resp.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);
        Assert.Equal("Resource not found", problem!.Title);
    }

    [Fact]
    public async Task DeleteDocument_NonExistent_Returns404WithResourceNotFoundProblem()
    {
        var resp = await _client.DeleteAsync("/api/documents/99999", Ct);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var problem = await resp.Content.ReadFromJsonAsync<ApiProblem>(Json, Ct);
        Assert.Equal("Resource not found", problem!.Title);
    }

    // List for a project that has no documents must return an empty array
    // (not 404 / not null) so the client can render a clean empty state.
    [Fact]
    public async Task GetDocuments_NoUploads_ReturnsEmptyArray()
    {
        var projectId = await CreateProjectAsync();

        var list = await (await _client.GetAsync(
            $"/api/projects/{projectId}/documents", Ct))
            .Content.ReadFromJsonAsync<ProjectDocumentDto[]>(Json, Ct);

        Assert.Empty(list!);
    }
}
