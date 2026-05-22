using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Tasks.Commands;

public class CreateProjectTaskCommandHandlerTests
{
    private readonly IProjectTaskRepository _taskRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly CreateProjectTaskCommandHandler _handler;

    public CreateProjectTaskCommandHandlerTests()
    {
        _taskRepo = Substitute.For<IProjectTaskRepository>();
        _projectRepo = Substitute.For<IProjectRepository>();
        _employeeRepo = Substitute.For<IEmployeeRepository>();
        _handler = new CreateProjectTaskCommandHandler(_taskRepo, _projectRepo, _employeeRepo);
    }

    private static Employee CreateEmployee(int id, string email = "person@example.com")
    {
        var employee = new Employee("First", "Last", "Patronymic", email);
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

    [Fact]
    public async Task Handle_ValidData_CreatesTaskAndReturnsId()
    {
        var pm = CreateEmployee(1);
        var participant = CreateEmployee(2, "p@example.com");
        var author = CreateEmployee(3, "a@example.com");
        var project = CreateProject(10, pm, participant);

        _projectRepo.GetProjectByIdAsync(10, Arg.Any<CancellationToken>()).Returns(project);
        _employeeRepo.GetEmployeesByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee> { author, participant });
        _taskRepo
            .When(x => x.AddTaskAsync(Arg.Any<ProjectTask>(), Arg.Any<CancellationToken>()))
            .Do(c => c.Arg<ProjectTask>().GetType().GetProperty("Id")!
                      .SetValue(c.Arg<ProjectTask>(), 77));

        var command = new CreateProjectTaskCommand
        {
            Data = new CreateProjectTaskRequest
            {
                ProjectId = 10,
                AuthorId = 3,
                AssigneeId = 2,
                Name = "Test Task",
                Priority = 4
            }
        };

        var response = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(77, response.Id);
        await _taskRepo.Received(1).AddTaskAsync(Arg.Any<ProjectTask>(), Arg.Any<CancellationToken>());
        await _taskRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProjectNotFound_ThrowsEntityNotFoundException()
    {
        _projectRepo.GetProjectByIdAsync(10, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var command = new CreateProjectTaskCommand
        {
            Data = new CreateProjectTaskRequest
            {
                ProjectId = 10, AuthorId = 1, AssigneeId = 1, Name = "Task"
            }
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        await _taskRepo.DidNotReceive().AddTaskAsync(Arg.Any<ProjectTask>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AuthorNotFound_ThrowsEntityNotFoundException()
    {
        var pm = CreateEmployee(1);
        var project = CreateProject(10, pm);

        _projectRepo.GetProjectByIdAsync(10, Arg.Any<CancellationToken>()).Returns(project);
        // Only the PM is returned (no author id 3)
        _employeeRepo.GetEmployeesByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee> { pm });

        var command = new CreateProjectTaskCommand
        {
            Data = new CreateProjectTaskRequest
            {
                ProjectId = 10, AuthorId = 3, AssigneeId = 1, Name = "Task"
            }
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AssigneeNotProjectMember_ThrowsDomainValidation()
    {
        var pm = CreateEmployee(1);
        var outsider = CreateEmployee(99, "out@example.com");
        var project = CreateProject(10, pm);

        _projectRepo.GetProjectByIdAsync(10, Arg.Any<CancellationToken>()).Returns(project);
        _employeeRepo.GetEmployeesByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee> { pm, outsider });

        var command = new CreateProjectTaskCommand
        {
            Data = new CreateProjectTaskRequest
            {
                ProjectId = 10, AuthorId = 1, AssigneeId = 99, Name = "Task"
            }
        };

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
