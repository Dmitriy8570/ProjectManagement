using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Tasks.Queries;

public class GetProjectTaskByIdQueryHandlerTests
{
    private readonly IProjectTaskRepository _taskRepo;
    private readonly GetProjectTaskByIdQueryHandler _handler;

    public GetProjectTaskByIdQueryHandlerTests()
    {
        _taskRepo = Substitute.For<IProjectTaskRepository>();
        _handler = new GetProjectTaskByIdQueryHandler(_taskRepo);
    }

    private static Employee CreateEmployee(int id)
    {
        var employee = new Employee("First", "Last", "Patronymic");
        typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(employee, id);
        return employee;
    }

    private static Project CreateProject(int id, Employee pm)
    {
        var project = new Project(
            "Test Project", "Customer Co", "Executing Co",
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), pm, priority: 3);
        typeof(Project).GetProperty(nameof(Project.Id))!.SetValue(project, id);
        return project;
    }

    [Fact]
    public async Task Handle_ExistingTask_ReturnsMappedDto()
    {
        var pm = CreateEmployee(1);
        var assignee = CreateEmployee(2);
        var project = CreateProject(10, pm);
        project.AddEmployee(assignee);

        var task = new ProjectTask(project, pm, assignee, "My Task", "A comment", 5);
        typeof(ProjectTask).GetProperty(nameof(ProjectTask.Id))!.SetValue(task, 42);

        _taskRepo.GetTaskByIdAsync(42, Arg.Any<CancellationToken>()).Returns(task);

        var result = await _handler.Handle(
            new GetProjectTaskByIdQuery { Id = 42 }, CancellationToken.None);

        Assert.Equal(42, result.Id);
        Assert.Equal("My Task", result.Name);
        Assert.Equal("A comment", result.Comment);
        Assert.Equal(5, result.Priority);
        Assert.Equal(10, result.ProjectId);
        Assert.Equal(1, result.Author.Id);
        Assert.Equal(2, result.Assignee.Id);
    }

    [Fact]
    public async Task Handle_TaskNotFound_ThrowsEntityNotFoundException()
    {
        _taskRepo.GetTaskByIdAsync(99, Arg.Any<CancellationToken>()).Returns((ProjectTask?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(
                new GetProjectTaskByIdQuery { Id = 99 }, CancellationToken.None));
    }
}
