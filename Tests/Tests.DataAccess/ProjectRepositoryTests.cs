using BusinessLogic.Employees;
using BusinessLogic.Projects;
using DataAccess.Repositories;

namespace Tests.DataAccess;

public class ProjectRepositoryTests : DatabaseTestBase
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Employee> AddPmAsync(string tag = "PM")
    {
        var pm = new Employee(tag, tag, tag, $"{tag.ToLower()}@x.com");
        Db.Employees.Add(pm);
        await Db.SaveChangesAsync(Ct);
        return pm;
    }

    private Project MakeProject(string name, Employee pm,
        DateTime? start = null, DateTime? end = null, int priority = 5) =>
        new(name, "c", "e",
            start ?? new DateTime(2024, 1, 1),
            end   ?? new DateTime(2024, 12, 31),
            pm, priority);

    // ── priority filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectsAsync_FiltersByPriorityRange()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("Low",  pm, priority: 1),
            MakeProject("Mid",  pm, priority: 5),
            MakeProject("High", pm, priority: 9));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { MinPriority = 3, MaxPriority = 7 }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Mid", page.Items[0].Name);
    }

    // ── date filters ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectsAsync_FiltersByStartDateFrom()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("Old",   pm, start: new(2023, 1, 1), end: new(2023, 12, 31)),
            MakeProject("New",   pm, start: new(2025, 1, 1), end: new(2025, 12, 31)));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { StartDateFrom = new(2024, 1, 1) }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("New", page.Items[0].Name);
    }

    [Fact]
    public async Task GetProjectsAsync_FiltersByStartDateTo()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("Old",   pm, start: new(2023, 1, 1), end: new(2023, 12, 31)),
            MakeProject("New",   pm, start: new(2025, 1, 1), end: new(2025, 12, 31)));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { StartDateTo = new(2024, 1, 1) }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Old", page.Items[0].Name);
    }

    [Fact]
    public async Task GetProjectsAsync_FiltersByEndDateFrom()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("Short", pm, start: new(2023, 1, 1), end: new(2023, 6, 30)),
            MakeProject("Long",  pm, start: new(2023, 1, 1), end: new(2025, 6, 30)));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { EndDateFrom = new(2024, 1, 1) }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Long", page.Items[0].Name);
    }

    [Fact]
    public async Task GetProjectsAsync_FiltersByEndDateTo()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("Short", pm, start: new(2023, 1, 1), end: new(2023, 6, 30)),
            MakeProject("Long",  pm, start: new(2023, 1, 1), end: new(2025, 6, 30)));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { EndDateTo = new(2024, 1, 1) }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Short", page.Items[0].Name);
    }

    // ── project-manager filter ────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectsAsync_FiltersByProjectManagerId()
    {
        var pm1 = await AddPmAsync("PM1");
        var pm2 = await AddPmAsync("PM2");
        Db.Projects.AddRange(
            MakeProject("Alpha", pm1),
            MakeProject("Beta",  pm2));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { ProjectManagerId = pm1.Id }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Alpha", page.Items[0].Name);
    }

    // ── TotalCount reflects the filter, not the full table ───────────────────

    [Fact]
    public async Task GetProjectsAsync_TotalCountReflectsFilter()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("A", pm, priority: 1),
            MakeProject("B", pm, priority: 5),
            MakeProject("C", pm, priority: 9));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { MaxPriority = 5 }, Ct);

        Assert.Equal(2, page.TotalCount);
    }

    // ── sorting ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectsAsync_SortsByNameAscending()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("Zeta",  pm),
            MakeProject("Alpha", pm),
            MakeProject("Mu",    pm));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { SortBy = ProjectSortBy.Name }, Ct);

        Assert.Equal(["Alpha", "Mu", "Zeta"], page.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task GetProjectsAsync_SortsByNameDescending()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("Zeta",  pm),
            MakeProject("Alpha", pm),
            MakeProject("Mu",    pm));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { SortBy = ProjectSortBy.Name, Descending = true }, Ct);

        Assert.Equal(["Zeta", "Mu", "Alpha"], page.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task GetProjectsAsync_SortsByPriorityAscending()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("High", pm, priority: 9),
            MakeProject("Low",  pm, priority: 1),
            MakeProject("Mid",  pm, priority: 5));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { SortBy = ProjectSortBy.Priority }, Ct);

        Assert.Equal([1, 5, 9], page.Items.Select(p => p.Priority));
    }

    [Fact]
    public async Task GetProjectsAsync_SortsByStartDateDescending()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("A", pm, start: new(2023, 1, 1), end: new(2023, 12, 31)),
            MakeProject("B", pm, start: new(2024, 1, 1), end: new(2024, 12, 31)),
            MakeProject("C", pm, start: new(2025, 1, 1), end: new(2025, 12, 31)));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { SortBy = ProjectSortBy.StartDate, Descending = true }, Ct);

        Assert.Equal(["C", "B", "A"], page.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task GetProjectsAsync_SortsByEndDateAscending()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(
            MakeProject("A", pm, start: new(2023, 1, 1), end: new(2025, 6, 30)),
            MakeProject("B", pm, start: new(2023, 1, 1), end: new(2023, 6, 30)),
            MakeProject("C", pm, start: new(2023, 1, 1), end: new(2024, 6, 30)));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { SortBy = ProjectSortBy.EndDate }, Ct);

        Assert.Equal(["B", "C", "A"], page.Items.Select(p => p.Name));
    }

    // ── paging ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectsAsync_PagingReturnsCorrectPage()
    {
        var pm = await AddPmAsync();
        Db.Projects.AddRange(Enumerable.Range(1, 5)
            .Select(i => MakeProject($"P{i}", pm, priority: i)));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(
            new ProjectListFilter { SortBy = ProjectSortBy.Priority, Page = 2, PageSize = 2 }, Ct);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["P3", "P4"], page.Items.Select(p => p.Name));
    }

    // ── includes (GetProjectsAsync) ───────────────────────────────────────────

    [Fact]
    public async Task GetProjectsAsync_IncludesProjectManager()
    {
        var pm = await AddPmAsync("Alice");
        Db.Projects.Add(MakeProject("X", pm));
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(new ProjectListFilter(), Ct);

        Assert.NotNull(page.Items[0].ProjectManager);
        Assert.Equal("Alice", page.Items[0].ProjectManager!.FirstName);
    }

    [Fact]
    public async Task GetProjectsAsync_IncludesEmployees()
    {
        var pm = await AddPmAsync();
        var emp = new Employee("Bob", "B", "B", "bob@x.com");
        Db.Employees.Add(emp);
        await Db.SaveChangesAsync(Ct);

        var project = MakeProject("Y", pm);
        project.AddEmployee(emp);
        Db.Projects.Add(project);
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var page = await sut.GetProjectsAsync(new ProjectListFilter(), Ct);

        Assert.Single(page.Items[0].Employees);
        Assert.Equal("Bob", page.Items[0].Employees.First().FirstName);
    }

    // ── includes (GetProjectByIdAsync) ────────────────────────────────────────

    [Fact]
    public async Task GetProjectByIdAsync_IncludesProjectManagerAndEmployees()
    {
        var pm = await AddPmAsync("Carol");
        var emp = new Employee("Dave", "D", "D", "dave@x.com");
        Db.Employees.Add(emp);
        await Db.SaveChangesAsync(Ct);

        var project = MakeProject("Z", pm);
        project.AddEmployee(emp);
        Db.Projects.Add(project);
        await Db.SaveChangesAsync(Ct);

        var sut = new ProjectRepository(Db);

        var result = await sut.GetProjectByIdAsync(project.Id, Ct);

        Assert.NotNull(result);
        Assert.NotNull(result!.ProjectManager);
        Assert.Equal("Carol", result.ProjectManager!.FirstName);
        Assert.Single(result.Employees);
        Assert.Equal("Dave", result.Employees.First().FirstName);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ReturnsNull_WhenNotFound()
    {
        var sut = new ProjectRepository(Db);

        var result = await sut.GetProjectByIdAsync(999, Ct);

        Assert.Null(result);
    }
}