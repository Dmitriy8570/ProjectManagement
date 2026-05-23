using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Projects.Commands;

public class AssignEmployeeToProjectCommandHandlerTests
{
    private readonly IProjectRepository _projectRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly AssignEmployeeToProjectCommandHandler _handler;

    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd = new(2024, 12, 31);

    public AssignEmployeeToProjectCommandHandlerTests()
    {
        _projectRepo = Substitute.For<IProjectRepository>();
        _employeeRepo = Substitute.For<IEmployeeRepository>();
        _handler = new AssignEmployeeToProjectCommandHandler(_projectRepo, _employeeRepo);
    }

    private static Employee CreateEmployee(int id, string email = "person@example.com")
    {
        var employee = new Employee("First", "Last", "Patronymic");
        typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(employee, id);
        return employee;
    }

    private static Project CreateProject(int id = 1, int pmId = 10)
    {
        var pm = CreateEmployee(pmId, $"pm{pmId}@example.com");
        var project = new Project("Name", "Customer", "Executing", DefaultStart, DefaultEnd, pm, 3);
        typeof(Project).GetProperty("Id")!.SetValue(project, id);
        return project;
    }

    [Fact]
    public async Task Handle_ProjectNotFound_ThrowsEntityNotFoundException()
    {
        _projectRepo.GetProjectByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Project?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(new AssignEmployeeToProjectCommand
            {
                Data = new AssignEmployeeToProjectRequest { ProjectId = 99, EmployeeId = 1 }
            }, CancellationToken.None));

        await _projectRepo.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmployeeNotFound_ThrowsEntityNotFoundException()
    {
        var project = CreateProject(id: 1, pmId: 10);
        _projectRepo.GetProjectByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);
        _employeeRepo.GetEmployeeByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(new AssignEmployeeToProjectCommand
            {
                Data = new AssignEmployeeToProjectRequest { ProjectId = 1, EmployeeId = 99 }
            }, CancellationToken.None));

        await _projectRepo.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidAssignment_AddsEmployeeToProjectAndSaves()
    {
        var project = CreateProject(id: 1, pmId: 10);
        var employee = CreateEmployee(id: 2, email: "emp@example.com");
        _projectRepo.GetProjectByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);
        _employeeRepo.GetEmployeeByIdAsync(2, Arg.Any<CancellationToken>()).Returns(employee);

        await _handler.Handle(new AssignEmployeeToProjectCommand
        {
            Data = new AssignEmployeeToProjectRequest { ProjectId = 1, EmployeeId = 2 }
        }, CancellationToken.None);

        Assert.Contains(employee, project.Employees);
        await _projectRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmployeeIsProjectManager_ThrowsDomainValidationException()
    {
        var project = CreateProject(id: 1, pmId: 10);
        var pm = CreateEmployee(id: 10, email: "pm10@example.com");
        _projectRepo.GetProjectByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);
        _employeeRepo.GetEmployeeByIdAsync(10, Arg.Any<CancellationToken>()).Returns(pm);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(new AssignEmployeeToProjectCommand
            {
                Data = new AssignEmployeeToProjectRequest { ProjectId = 1, EmployeeId = 10 }
            }, CancellationToken.None));

        await _projectRepo.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmployeeAlreadyAssigned_IsNoOpAndStillSaves()
    {
        var project = CreateProject(id: 1, pmId: 10);
        var employee = CreateEmployee(id: 2, email: "emp@example.com");
        project.AddEmployee(employee);
        _projectRepo.GetProjectByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);
        _employeeRepo.GetEmployeeByIdAsync(2, Arg.Any<CancellationToken>()).Returns(employee);

        await _handler.Handle(new AssignEmployeeToProjectCommand
        {
            Data = new AssignEmployeeToProjectRequest { ProjectId = 1, EmployeeId = 2 }
        }, CancellationToken.None);

        Assert.Single(project.Employees);
        await _projectRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }
}
