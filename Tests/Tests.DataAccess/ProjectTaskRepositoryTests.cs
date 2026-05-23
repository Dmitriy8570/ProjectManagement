using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;
using DataAccess.Repositories;

namespace Tests.DataAccess;

public class ProjectTaskRepositoryTests : DatabaseTestBase
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Employee> AddEmployeeAsync(string tag = "Emp")
    {
        var emp = new Employee(tag, tag, tag);
        Db.Employees.Add(emp);
        await Db.SaveChangesAsync(Ct);
        return emp;
    }

    private async Task<Project> AddProjectAsync(Employee pm, string name = "Project", params Employee[] participants)
    {
        var project = new Project(name, "c", "e",
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), pm, priority: 5);
        foreach (var p in participants)
            project.AddEmployee(p);
        Db.Projects.Add(project);
        await Db.SaveChangesAsync(Ct);
        return project;
    }

    private async Task<ProjectTask> AddTaskAsync(
        Project project, Employee author, Employee assignee,
        string name = "Task", int priority = 1,
        ProjectTaskStatus status = ProjectTaskStatus.ToDo)
    {
        var task = new ProjectTask(project, author, assignee, name, null, priority, status);
        Db.ProjectTasks.Add(task);
        await Db.SaveChangesAsync(Ct);
        return task;
    }

    // ── filters ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTasksAsync_FiltersByProjectId()
    {
        var pm = await AddEmployeeAsync("PM");
        var p1 = await AddProjectAsync(pm, "P1");
        var p2 = await AddProjectAsync(pm, "P2");
        await AddTaskAsync(p1, pm, pm, "T1");
        await AddTaskAsync(p2, pm, pm, "T2");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { ProjectId = p1.Id }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("T1", page.Items[0].Name);
    }

    [Fact]
    public async Task GetTasksAsync_FiltersByAssigneeId()
    {
        var pm = await AddEmployeeAsync("PM");
        var emp = await AddEmployeeAsync("Worker");
        var project = await AddProjectAsync(pm, participants: emp);
        await AddTaskAsync(project, pm, pm, "PM Task");
        await AddTaskAsync(project, pm, emp, "Emp Task");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { AssigneeId = emp.Id }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Emp Task", page.Items[0].Name);
    }

    [Fact]
    public async Task GetTasksAsync_FiltersByAuthorId()
    {
        var pm = await AddEmployeeAsync("PM");
        var emp = await AddEmployeeAsync("Author");
        var project = await AddProjectAsync(pm, participants: emp);
        await AddTaskAsync(project, pm, pm, "PM authored");
        await AddTaskAsync(project, emp, pm, "Emp authored");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { AuthorId = emp.Id }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Emp authored", page.Items[0].Name);
    }

    [Fact]
    public async Task GetTasksAsync_FiltersByStatus()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        await AddTaskAsync(project, pm, pm, "ToDo", status: ProjectTaskStatus.ToDo);
        await AddTaskAsync(project, pm, pm, "Done", status: ProjectTaskStatus.Done);

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { Status = ProjectTaskStatus.Done }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Done", page.Items[0].Name);
    }

    [Fact]
    public async Task GetTasksAsync_FiltersByPriorityRange()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        await AddTaskAsync(project, pm, pm, "Low", priority: 1);
        await AddTaskAsync(project, pm, pm, "Mid", priority: 5);
        await AddTaskAsync(project, pm, pm, "High", priority: 9);

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { MinPriority = 3, MaxPriority = 7 }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Mid", page.Items[0].Name);
    }

    [Fact]
    public async Task GetTasksAsync_FiltersByNameSearch()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        await AddTaskAsync(project, pm, pm, "Fix login bug");
        await AddTaskAsync(project, pm, pm, "Add dashboard");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { NameSearch = "login" }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Fix login bug", page.Items[0].Name);
    }

    [Fact]
    public async Task GetTasksAsync_FiltersByProjectManagerId()
    {
        var pm1 = await AddEmployeeAsync("PM1");
        var pm2 = await AddEmployeeAsync("PM2");
        var p1 = await AddProjectAsync(pm1, "P1");
        var p2 = await AddProjectAsync(pm2, "P2");
        await AddTaskAsync(p1, pm1, pm1, "T1");
        await AddTaskAsync(p2, pm2, pm2, "T2");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { ProjectManagerId = pm1.Id }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("T1", page.Items[0].Name);
    }

    [Fact]
    public async Task GetTasksAsync_FiltersByParticipantEmployeeId()
    {
        var pm = await AddEmployeeAsync("PM");
        var emp = await AddEmployeeAsync("Worker");
        var projectIn = await AddProjectAsync(pm, "In", emp);
        var projectOut = await AddProjectAsync(pm, "Out");
        await AddTaskAsync(projectIn, pm, emp, "Visible");
        await AddTaskAsync(projectOut, pm, pm, "Hidden");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { ParticipantEmployeeId = emp.Id }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("Visible", page.Items[0].Name);
    }

    [Fact]
    public async Task GetTasksAsync_ParticipantFilter_IncludesPmTasks()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        await AddTaskAsync(project, pm, pm, "PM Task");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { ParticipantEmployeeId = pm.Id }, Ct);

        Assert.Single(page.Items);
        Assert.Equal("PM Task", page.Items[0].Name);
    }

    // ── sorting ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTasksAsync_SortsByNameAscending()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        await AddTaskAsync(project, pm, pm, "Zeta");
        await AddTaskAsync(project, pm, pm, "Alpha");
        await AddTaskAsync(project, pm, pm, "Mu");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { SortBy = ProjectTaskSortBy.Name }, Ct);

        Assert.Equal(new[] { "Alpha", "Mu", "Zeta" }, page.Items.Select(t => t.Name));
    }

    [Fact]
    public async Task GetTasksAsync_SortsByPriorityDescending()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        await AddTaskAsync(project, pm, pm, "Low", priority: 1);
        await AddTaskAsync(project, pm, pm, "High", priority: 9);
        await AddTaskAsync(project, pm, pm, "Mid", priority: 5);

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { SortBy = ProjectTaskSortBy.Priority, Descending = true }, Ct);

        Assert.Equal(new[] { 9, 5, 1 }, page.Items.Select(t => t.Priority));
    }

    [Fact]
    public async Task GetTasksAsync_SortsByStatus()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        await AddTaskAsync(project, pm, pm, "Done", status: ProjectTaskStatus.Done);
        await AddTaskAsync(project, pm, pm, "ToDo", status: ProjectTaskStatus.ToDo);
        await AddTaskAsync(project, pm, pm, "InProgress", status: ProjectTaskStatus.InProgress);

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { SortBy = ProjectTaskSortBy.Status }, Ct);

        var statuses = page.Items.Select(t => t.Status).ToArray();
        Assert.Equal(3, statuses.Length);
        Assert.True(
            string.Compare(statuses[0].ToString(), statuses[1].ToString(), StringComparison.Ordinal) <= 0
            && string.Compare(statuses[1].ToString(), statuses[2].ToString(), StringComparison.Ordinal) <= 0,
            "Status sort should be consistent ascending order");
    }

    // ── paging ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTasksAsync_PagingReturnsCorrectPage()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        for (var i = 1; i <= 5; i++)
            await AddTaskAsync(project, pm, pm, $"T{i}", priority: i);

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(
            new ProjectTaskListFilter { SortBy = ProjectTaskSortBy.Priority, Page = 2, PageSize = 2 }, Ct);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    // ── includes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTasksAsync_IncludesProjectAuthorAssignee()
    {
        var pm = await AddEmployeeAsync("PM");
        var emp = await AddEmployeeAsync("Worker");
        var project = await AddProjectAsync(pm, participants: emp);
        await AddTaskAsync(project, pm, emp, "Task");

        var sut = new ProjectTaskRepository(Db);
        var page = await sut.GetTasksAsync(new ProjectTaskListFilter(), Ct);

        var task = Assert.Single(page.Items);
        Assert.NotNull(task.Project);
        Assert.NotNull(task.Author);
        Assert.NotNull(task.Assignee);
        Assert.Equal("PM", task.Author!.FirstName);
        Assert.Equal("Worker", task.Assignee!.FirstName);
    }

    [Fact]
    public async Task GetTaskByIdAsync_IncludesProjectWithPmAndEmployees()
    {
        var pm = await AddEmployeeAsync("PM");
        var emp = await AddEmployeeAsync("Worker");
        var project = await AddProjectAsync(pm, participants: emp);
        var task = await AddTaskAsync(project, pm, emp);

        var sut = new ProjectTaskRepository(Db);
        var result = await sut.GetTaskByIdAsync(task.Id, Ct);

        Assert.NotNull(result);
        Assert.NotNull(result!.Project);
        Assert.NotNull(result.Project!.ProjectManager);
        Assert.Single(result.Project.Employees);
    }

    [Fact]
    public async Task GetTaskByIdAsync_ReturnsNull_WhenNotFound()
    {
        var sut = new ProjectTaskRepository(Db);
        var result = await sut.GetTaskByIdAsync(999, Ct);
        Assert.Null(result);
    }

    // ── delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTaskAsync_ExistingTask_ReturnsTrue()
    {
        var pm = await AddEmployeeAsync("PM");
        var project = await AddProjectAsync(pm);
        var task = await AddTaskAsync(project, pm, pm);

        var sut = new ProjectTaskRepository(Db);
        var deleted = await sut.DeleteTaskAsync(task.Id, Ct);

        Assert.True(deleted);
    }

    [Fact]
    public async Task DeleteTaskAsync_NonExistent_ReturnsFalse()
    {
        var sut = new ProjectTaskRepository(Db);
        var deleted = await sut.DeleteTaskAsync(999, Ct);
        Assert.False(deleted);
    }

    // ── IsReferencedByAnyTaskAsync ───────────────────────────────────────────

    [Fact]
    public async Task IsReferencedByAnyTaskAsync_AssignedEmployee_ReturnsTrue()
    {
        var pm = await AddEmployeeAsync("PM");
        var emp = await AddEmployeeAsync("Worker");
        var project = await AddProjectAsync(pm, participants: emp);
        await AddTaskAsync(project, pm, emp);

        var sut = new ProjectTaskRepository(Db);
        Assert.True(await sut.IsReferencedByAnyTaskAsync(emp.Id, Ct));
    }

    [Fact]
    public async Task IsReferencedByAnyTaskAsync_UnreferencedEmployee_ReturnsFalse()
    {
        var pm = await AddEmployeeAsync("PM");
        var emp = await AddEmployeeAsync("Unused");

        var sut = new ProjectTaskRepository(Db);
        Assert.False(await sut.IsReferencedByAnyTaskAsync(emp.Id, Ct));
    }
}
