using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Projects.Commands;

public class UnassignEmployeeFromProjectCommandHandlerTests
{
    private readonly IProjectRepository _repository;
    private readonly UnassignEmployeeFromProjectCommandHandler _handler;

    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd = new(2024, 12, 31);

    public UnassignEmployeeFromProjectCommandHandlerTests()
    {
        _repository = Substitute.For<IProjectRepository>();
        _handler = new UnassignEmployeeFromProjectCommandHandler(_repository);
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
        _repository.GetProjectByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Project?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(new UnassignEmployeeFromProjectCommand
            {
                Data = new UnassignEmployeeFromProjectRequest { ProjectId = 99, EmployeeId = 1 }
            }, CancellationToken.None));

        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmployeeIsProjectManager_ThrowsDomainValidationException()
    {
        var project = CreateProject(id: 1, pmId: 10);
        _repository.GetProjectByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(new UnassignEmployeeFromProjectCommand
            {
                Data = new UnassignEmployeeFromProjectRequest { ProjectId = 1, EmployeeId = 10 }
            }, CancellationToken.None));

        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidUnassignment_RemovesEmployeeAndSaves()
    {
        var project = CreateProject(id: 1, pmId: 10);
        var employee = CreateEmployee(id: 2, email: "emp@example.com");
        project.AddEmployee(employee);
        _repository.GetProjectByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);

        await _handler.Handle(new UnassignEmployeeFromProjectCommand
        {
            Data = new UnassignEmployeeFromProjectRequest { ProjectId = 1, EmployeeId = 2 }
        }, CancellationToken.None);

        Assert.Empty(project.Employees);
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmployeeNotInProject_IsNoOpAndStillSaves()
    {
        var project = CreateProject(id: 1, pmId: 10);
        _repository.GetProjectByIdAsync(1, Arg.Any<CancellationToken>()).Returns(project);

        await _handler.Handle(new UnassignEmployeeFromProjectCommand
        {
            Data = new UnassignEmployeeFromProjectRequest { ProjectId = 1, EmployeeId = 999 }
        }, CancellationToken.None);

        Assert.Empty(project.Employees);
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }
}
