using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Tasks.Commands;

public class ChangeProjectTaskStatusCommandHandlerTests
{
    private readonly IProjectTaskRepository _taskRepo;
    private readonly ChangeProjectTaskStatusCommandHandler _handler;

    public ChangeProjectTaskStatusCommandHandlerTests()
    {
        _taskRepo = Substitute.For<IProjectTaskRepository>();
        _handler = new ChangeProjectTaskStatusCommandHandler(_taskRepo);
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
    public async Task Handle_ValidTransition_UpdatesStatusAndSaves()
    {
        var pm = CreateEmployee(1);
        var project = CreateProject(10, pm);
        var task = new ProjectTask(project, pm, pm, "Task", null, 1);
        typeof(ProjectTask).GetProperty(nameof(ProjectTask.Id))!.SetValue(task, 42);

        _taskRepo.GetTaskByIdAsync(42, Arg.Any<CancellationToken>()).Returns(task);

        var command = new ChangeProjectTaskStatusCommand
        {
            Id = 42,
            Status = ProjectTaskStatus.InProgress
        };

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ProjectTaskStatus.InProgress, task.Status);
        await _taskRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TaskNotFound_ThrowsEntityNotFoundException()
    {
        _taskRepo.GetTaskByIdAsync(99, Arg.Any<CancellationToken>()).Returns((ProjectTask?)null);

        var command = new ChangeProjectTaskStatusCommand
        {
            Id = 99,
            Status = ProjectTaskStatus.Done
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ToDoToInProgressToDone_AllTransitionsWork()
    {
        var pm = CreateEmployee(1);
        var project = CreateProject(10, pm);
        var task = new ProjectTask(project, pm, pm, "Task", null, 1);
        typeof(ProjectTask).GetProperty(nameof(ProjectTask.Id))!.SetValue(task, 42);

        _taskRepo.GetTaskByIdAsync(42, Arg.Any<CancellationToken>()).Returns(task);

        Assert.Equal(ProjectTaskStatus.ToDo, task.Status);

        await _handler.Handle(
            new ChangeProjectTaskStatusCommand { Id = 42, Status = ProjectTaskStatus.InProgress },
            CancellationToken.None);
        Assert.Equal(ProjectTaskStatus.InProgress, task.Status);

        await _handler.Handle(
            new ChangeProjectTaskStatusCommand { Id = 42, Status = ProjectTaskStatus.Done },
            CancellationToken.None);
        Assert.Equal(ProjectTaskStatus.Done, task.Status);
    }
}
