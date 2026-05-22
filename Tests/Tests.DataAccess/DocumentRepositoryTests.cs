using BusinessLogic.Documents;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Tests.DataAccess;

/// <summary>
/// In-memory SQLite tests for DocumentRepository — proves the EF Core queries
/// actually execute and that the configuration (FK, indexes, cascade) lines
/// up with how the repository reads/writes documents.
/// </summary>
public class DocumentRepositoryTests : DatabaseTestBase
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> SeedProjectAsync(string name = "P")
    {
        var pm = new Employee("Ivan", "Petrov", "Sergeevich", $"{Guid.NewGuid():N}@x.com");
        Db.Employees.Add(pm);
        await Db.SaveChangesAsync(Ct);

        var project = new Project(name,
            customerCompany: "C", executingCompany: "E",
            startDate: new DateTime(2026, 1, 1),
            endDate:   new DateTime(2026, 12, 31),
            projectManager: pm, priority: 1);
        Db.Projects.Add(project);
        await Db.SaveChangesAsync(Ct);
        return project.Id;
    }

    private static ProjectDocument NewDocument(
        int projectId,
        string fileName = "report.pdf",
        string storedName = "stored.pdf",
        string contentType = "application/pdf",
        long sizeBytes = 100) =>
        new(projectId, fileName, storedName, contentType, sizeBytes);

    // ── AddAsync + SaveAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsDocument_VisibleViaGetById()
    {
        var projectId = await SeedProjectAsync();
        var sut = new DocumentRepository(Db);

        var doc = NewDocument(projectId);
        await sut.AddAsync(doc, Ct);
        await sut.SaveAsync(Ct);

        var fetched = await sut.GetByIdAsync(doc.Id, Ct);
        Assert.NotNull(fetched);
        Assert.Equal("report.pdf", fetched!.FileName);
        Assert.Equal(projectId,    fetched.ProjectId);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var sut = new DocumentRepository(Db);

        Assert.Null(await sut.GetByIdAsync(9999, Ct));
    }

    // ── GetByProjectIdAsync ──────────────────────────────────────────────────

    // Documents of other projects must not appear, and ordering is newest-first
    // by UploadedAt so the most recent uploads sit at the top of the list.
    [Fact]
    public async Task GetByProjectIdAsync_FiltersByProject_AndOrdersNewestFirst()
    {
        var p1 = await SeedProjectAsync("Project1");
        var p2 = await SeedProjectAsync("Project2");

        var sut = new DocumentRepository(Db);

        var older = NewDocument(p1, fileName: "old.txt",   storedName: "old.bin");
        await sut.AddAsync(older, Ct);
        await sut.SaveAsync(Ct);

        // Bump UploadedAt by an obvious margin so the order assertion is
        // robust against same-tick timestamps.
        await Task.Delay(20, Ct);

        var newer = NewDocument(p1, fileName: "new.txt",   storedName: "new.bin");
        await sut.AddAsync(newer, Ct);
        await sut.SaveAsync(Ct);

        var otherProjectDoc = NewDocument(p2, fileName: "other.txt", storedName: "other.bin");
        await sut.AddAsync(otherProjectDoc, Ct);
        await sut.SaveAsync(Ct);

        var p1Docs = await sut.GetByProjectIdAsync(p1, Ct);

        Assert.Equal(2, p1Docs.Count);
        Assert.Equal(new[] { "new.txt", "old.txt" }, p1Docs.Select(d => d.FileName));
        Assert.DoesNotContain(p1Docs, d => d.FileName == "other.txt");
    }

    [Fact]
    public async Task GetByProjectIdAsync_NoDocuments_ReturnsEmptyList()
    {
        var projectId = await SeedProjectAsync();
        var sut = new DocumentRepository(Db);

        var result = await sut.GetByProjectIdAsync(projectId, Ct);

        Assert.Empty(result);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingDocument_ReturnsTrueAndRemovesAfterSave()
    {
        var projectId = await SeedProjectAsync();
        var sut = new DocumentRepository(Db);

        var doc = NewDocument(projectId);
        await sut.AddAsync(doc, Ct);
        await sut.SaveAsync(Ct);

        var removed = await sut.DeleteAsync(doc.Id, Ct);
        await sut.SaveAsync(Ct);

        Assert.True(removed);
        Assert.Null(await sut.GetByIdAsync(doc.Id, Ct));
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ReturnsFalse()
    {
        var sut = new DocumentRepository(Db);

        Assert.False(await sut.DeleteAsync(9999, Ct));
    }

    // The mapping configures Cascade on the project FK; deleting the parent
    // project must also remove its documents. This proves the configuration
    // is wired up rather than just documented.
    [Fact]
    public async Task DeletingProject_CascadesAndRemovesItsDocuments()
    {
        var projectId = await SeedProjectAsync();
        var sut = new DocumentRepository(Db);

        await sut.AddAsync(NewDocument(projectId, storedName: "d1.bin"), Ct);
        await sut.AddAsync(NewDocument(projectId, storedName: "d2.bin"), Ct);
        await sut.SaveAsync(Ct);

        var project = await Db.Projects.FirstAsync(p => p.Id == projectId, Ct);
        Db.Projects.Remove(project);
        await Db.SaveChangesAsync(Ct);

        Assert.Empty(await sut.GetByProjectIdAsync(projectId, Ct));
    }
}
