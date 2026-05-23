using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Tasks.Commands;

public class EditProjectTaskCommandHandlerTests
{
    private readonly IProjectTaskRepository _taskRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly EditProjectTaskCommandHandler _handler;

    public EditProjectTaskCommandHandlerTests()
    {
        _taskRepo = Substitute.For<IProjectTaskRepository>();
        _employeeRepo = Substitute.For<IEmployeeRepository>();
        _handler = new EditProjectTaskCommandHandler(_taskRepo, _employeeRepo);
    }

    private static Employee CreateEmployee(int id)
    {
        var employee = new Employee("First", "Last", "Patronymic");
        typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(employee, id);
        return employee;
    }

    private static Project CreateProject(int id, Employee pm, params Employee[] participants)
    {
        var project = new Project(
            "Test Project", "Customer Co", "Executing Co",
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), pm, priority: 3);
        typeof(Project).GetProperty(nameof(Project.Id))!.SetValue(project, id);
        foreach (var p in participants)
            project.AddEmployee(p);
        return project;
    }

    private static ProjectTask CreateTask(int id, Project project, Employee author, Employee assignee)
    {
        var task = new ProjectTask(project, author, assignee, "Original", null, 1);
        typeof(ProjectTask).GetProperty(nameof(ProjectTask.Id))!.SetValue(task, id);
        return task;
    }

    [Fact]
    public async Task Handle_UpdateNameOnly_SavesAndReturnsId()
    {
        var pm = CreateEmployee(1);
        var project = CreateProject(10, pm);
        var task = CreateTask(42, project, pm, pm);

        _taskRepo.GetTaskByIdAsync(42, Arg.Any<CancellationToken>()).Returns(task);

        var command = new EditProjectTaskCommand
        {
            Id = 42,
            Data = new EditProjectTaskRequest { Name = "Renamed" }
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(42, result.Id);
        await _taskRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
        await _employeeRepo.DidNotReceive()
            .GetEmployeeByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TaskNotFound_ThrowsEntityNotFoundException()
    {
        _taskRepo.GetTaskByIdAsync(99, Arg.Any<CancellationToken>()).Returns((ProjectTask?)null);

        var command = new EditProjectTaskCommand
        {
            Id = 99,
            Data = new EditProjectTaskRequest { Name = "X" }
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ChangeAssigneeToProjectMember_Succeeds()
    {
        var pm = CreateEmployee(1);
        var member = CreateEmployee(2);
        var project = CreateProject(10, pm, member);
        var task = CreateTask(42, project, pm, pm);

        _taskRepo.GetTaskByIdAsync(42, Arg.Any<CancellationToken>()).Returns(task);
        _employeeRepo.GetEmployeeByIdAsync(2, Arg.Any<CancellationToken>()).Returns(member);

        var command = new EditProjectTaskCommand
        {
            Id = 42,
            Data = new EditProjectTaskRequest { AssigneeId = 2 }
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(42, result.Id);
        await _taskRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ChangeAssigneeToNonMember_ThrowsDomainValidation()
    {
        var pm = CreateEmployee(1);
        var outsider = CreateEmployee(99);
        var project = CreateProject(10, pm);
        var task = CreateTask(42, project, pm, pm);

        _taskRepo.GetTaskByIdAsync(42, Arg.Any<CancellationToken>()).Returns(task);
        _employeeRepo.GetEmployeeByIdAsync(99, Arg.Any<CancellationToken>()).Returns(outsider);

        var command = new EditProjectTaskCommand
        {
            Id = 42,
            Data = new EditProjectTaskRequest { AssigneeId = 99 }
        };

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NewAssigneeNotFound_ThrowsEntityNotFoundException()
    {
        var pm = CreateEmployee(1);
        var project = CreateProject(10, pm);
        var task = CreateTask(42, project, pm, pm);

        _taskRepo.GetTaskByIdAsync(42, Arg.Any<CancellationToken>()).Returns(task);
        _employeeRepo.GetEmployeeByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        var command = new EditProjectTaskCommand
        {
            Id = 42,
            Data = new EditProjectTaskRequest { AssigneeId = 999 }
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameAssigneeId_SkipsReassignment()
    {
        var pm = CreateEmployee(1);
        var project = CreateProject(10, pm);
        var task = CreateTask(42, project, pm, pm);

        _taskRepo.GetTaskByIdAsync(42, Arg.Any<CancellationToken>()).Returns(task);

        var command = new EditProjectTaskCommand
        {
            Id = 42,
            Data = new EditProjectTaskRequest { AssigneeId = 1 }
        };

        await _handler.Handle(command, CancellationToken.None);

        await _employeeRepo.DidNotReceive()
            .GetEmployeeByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
