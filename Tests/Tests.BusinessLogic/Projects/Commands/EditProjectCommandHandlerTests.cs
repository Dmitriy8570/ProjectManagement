using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Identity;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Projects.Commands;

public class EditProjectCommandHandlerTests
{
    private readonly IProjectRepository _projectRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IUserAccountService _userAccountService;
    private readonly EditProjectCommandHandler _handler;

    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd = new(2024, 12, 31);

    public EditProjectCommandHandlerTests()
    {
        _projectRepo = Substitute.For<IProjectRepository>();
        _employeeRepo = Substitute.For<IEmployeeRepository>();
        _userAccountService = Substitute.For<IUserAccountService>();
        _handler = new EditProjectCommandHandler(_projectRepo, _employeeRepo, _userAccountService);
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
            () => _handler.Handle(new EditProjectCommand { Id = 99, Data = new EditProjectRequest() }, CancellationToken.None));

        await _projectRepo.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidUpdate_MutatesProjectAndSaves()
    {
        var project = CreateProject(id: 5);
        _projectRepo.GetProjectByIdAsync(5, Arg.Any<CancellationToken>()).Returns(project);

        var response = await _handler.Handle(new EditProjectCommand
        {
            Id = 5,
            Data = new EditProjectRequest { Name = "New Name", Priority = 7 }
        }, CancellationToken.None);

        Assert.Equal(5, response.Id);
        Assert.Equal("New Name", project.Name);
        Assert.Equal(7, project.Priority);
        await _projectRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AllNullFields_StillCallsSave()
    {
        var project = CreateProject(id: 3);
        _projectRepo.GetProjectByIdAsync(3, Arg.Any<CancellationToken>()).Returns(project);

        var response = await _handler.Handle(new EditProjectCommand
        {
            Id = 3,
            Data = new EditProjectRequest()
        }, CancellationToken.None);

        Assert.Equal(3, response.Id);
        await _projectRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewProjectManagerId_LoadsFromRepositoryAndUpdatesProject()
    {
        var project = CreateProject(id: 5, pmId: 10);
        var newPm = CreateEmployee(id: 20, email: "newpm@example.com");
        _projectRepo.GetProjectByIdAsync(5, Arg.Any<CancellationToken>()).Returns(project);
        _employeeRepo.GetEmployeeByIdAsync(20, Arg.Any<CancellationToken>()).Returns(newPm);

        await _handler.Handle(new EditProjectCommand
        {
            Id = 5,
            Data = new EditProjectRequest { ProjectManagerId = 20 }
        }, CancellationToken.None);

        await _employeeRepo.Received(1).GetEmployeeByIdAsync(20, Arg.Any<CancellationToken>());
        Assert.Equal(20, project.ProjectManagerId);
    }

    [Fact]
    public async Task Handle_SameProjectManagerId_DoesNotLoadFromRepository()
    {
        var project = CreateProject(id: 5, pmId: 10);
        _projectRepo.GetProjectByIdAsync(5, Arg.Any<CancellationToken>()).Returns(project);

        await _handler.Handle(new EditProjectCommand
        {
            Id = 5,
            Data = new EditProjectRequest { ProjectManagerId = 10 }
        }, CancellationToken.None);

        await _employeeRepo.DidNotReceive().GetEmployeeByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewProjectManagerNotFound_ThrowsEntityNotFoundException()
    {
        var project = CreateProject(id: 5, pmId: 10);
        _projectRepo.GetProjectByIdAsync(5, Arg.Any<CancellationToken>()).Returns(project);
        _employeeRepo.GetEmployeeByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(new EditProjectCommand
            {
                Id = 5,
                Data = new EditProjectRequest { ProjectManagerId = 99 }
            }, CancellationToken.None));

        await _projectRepo.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BlankName_ThrowsDomainValidationException()
    {
        var project = CreateProject(id: 5);
        _projectRepo.GetProjectByIdAsync(5, Arg.Any<CancellationToken>()).Returns(project);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _handler.Handle(new EditProjectCommand
            {
                Id = 5,
                Data = new EditProjectRequest { Name = "" }
            }, CancellationToken.None));

        await _projectRepo.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
