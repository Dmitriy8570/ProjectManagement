using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;

namespace Tests.BusinessLogic.Tasks;

public class ProjectTaskTests
{
    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd = new(2024, 12, 31);

    private static Employee CreateEmployee(int id)
    {
        var employee = new Employee("First", "Last", "Patronymic");
        typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(employee, id);
        return employee;
    }

    private static Project CreateProject(Employee pm, params Employee[] participants)
    {
        var project = new Project(
            "Test Project", "Customer Co", "Executing Co",
            DefaultStart, DefaultEnd, pm, priority: 3);
        foreach (var p in participants)
            project.AddEmployee(p);
        return project;
    }

    // ---------- Constructor ----------

    [Fact]
    public void Constructor_WithValidData_PopulatesAllFields()
    {
        var pm = CreateEmployee(id: 1);
        var participant = CreateEmployee(id: 2);
        var project = CreateProject(pm, participant);
        var author = CreateEmployee(id: 3);

        var task = new ProjectTask(project, author, participant, "Migrate DB", "Some comment", priority: 5);

        Assert.Equal("Migrate DB", task.Name);
        Assert.Equal("Some comment", task.Comment);
        Assert.Equal(5, task.Priority);
        Assert.Equal(ProjectTaskStatus.ToDo, task.Status);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Same(author, task.Author);
        Assert.Same(participant, task.Assignee);
    }

    [Fact]
    public void Constructor_WithProjectManagerAsAssignee_IsAllowed()
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);

        var task = new ProjectTask(project, author, pm, "Task", null, priority: 0);

        Assert.Same(pm, task.Assignee);
    }

    [Fact]
    public void Constructor_WithAssigneeOutsideProject_Throws()
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);
        var outsider = CreateEmployee(id: 99);

        Assert.Throws<DomainValidationException>(
            () => new ProjectTask(project, author, outsider, "Task", null, priority: 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankName_Throws(string invalidName)
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);

        Assert.Throws<DomainValidationException>(
            () => new ProjectTask(project, author, pm, invalidName, null, 0));
    }

    [Fact]
    public void Constructor_WithNegativePriority_Throws()
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);

        Assert.Throws<DomainValidationException>(
            () => new ProjectTask(project, author, pm, "Task", null, -1));
    }

    [Fact]
    public void Constructor_NullComment_StoredAsEmptyString()
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);

        var task = new ProjectTask(project, author, pm, "Task", null, 0);

        Assert.Equal(string.Empty, task.Comment);
    }

    // ---------- AssignWorker ----------

    [Fact]
    public void AssignWorker_ToProjectParticipant_ReplacesAssignee()
    {
        var pm = CreateEmployee(id: 1);
        var p1 = CreateEmployee(id: 2);
        var p2 = CreateEmployee(id: 3);
        var project = CreateProject(pm, p1, p2);
        var author = CreateEmployee(id: 4);
        var task = new ProjectTask(project, author, p1, "Task", null, 0);

        task.AssignWorker(p2);

        Assert.Same(p2, task.Assignee);
    }

    [Fact]
    public void AssignWorker_ToOutsider_Throws()
    {
        var pm = CreateEmployee(id: 1);
        var p1 = CreateEmployee(id: 2);
        var project = CreateProject(pm, p1);
        var author = CreateEmployee(id: 3);
        var outsider = CreateEmployee(id: 99);
        var task = new ProjectTask(project, author, p1, "Task", null, 0);

        Assert.Throws<DomainValidationException>(() => task.AssignWorker(outsider));
    }

    [Fact]
    public void AssignWorker_WithNull_Throws()
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);
        var task = new ProjectTask(project, author, pm, "Task", null, 0);

        Assert.Throws<ArgumentNullException>(() => task.AssignWorker(null!));
    }

    // ---------- Update / ChangeStatus ----------

    [Fact]
    public void Update_WithAllNull_LeavesFieldsUnchanged()
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);
        var task = new ProjectTask(project, author, pm, "Task", "Original", 3);

        task.Update();

        Assert.Equal("Task", task.Name);
        Assert.Equal("Original", task.Comment);
        Assert.Equal(3, task.Priority);
        Assert.Equal(ProjectTaskStatus.ToDo, task.Status);
    }

    [Fact]
    public void Update_AppliesEachProvidedField()
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);
        var task = new ProjectTask(project, author, pm, "Old", "Old comment", 1);

        task.Update(
            name: "New",
            comment: "New comment",
            priority: 7,
            status: ProjectTaskStatus.InProgress);

        Assert.Equal("New", task.Name);
        Assert.Equal("New comment", task.Comment);
        Assert.Equal(7, task.Priority);
        Assert.Equal(ProjectTaskStatus.InProgress, task.Status);
    }

    [Fact]
    public void ChangeStatus_SetsStatus()
    {
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(pm);
        var author = CreateEmployee(id: 2);
        var task = new ProjectTask(project, author, pm, "Task", null, 0);

        task.ChangeStatus(ProjectTaskStatus.Done);

        Assert.Equal(ProjectTaskStatus.Done, task.Status);
    }
}
