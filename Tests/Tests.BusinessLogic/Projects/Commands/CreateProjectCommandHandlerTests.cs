using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Commands;
using NSubstitute;

namespace Tests.BusinessLogic.Projects.Commands;

public class CreateProjectCommandHandlerTests
{
    private readonly IProjectRepository _projectRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly CreateProjectCommandHandler _handler;

    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd = new(2024, 12, 31);

    public CreateProjectCommandHandlerTests()
    {
        _projectRepo = Substitute.For<IProjectRepository>();
        _employeeRepo = Substitute.For<IEmployeeRepository>();
        _handler = new CreateProjectCommandHandler(_projectRepo, _employeeRepo);
    }

    private static Employee CreateEmployee(int id, string email = "person@example.com")
    {
        var employee = new Employee("First", "Last", "Patronymic", email);
        typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(employee, id);
        return employee;
    }

    private CreateProjectCommand CreateCommand(
        string name = "Test Project",
        string customerCompany = "Customer Co",
        string executingCompany = "Executing Co",
        int projectManagerId = 1,
        List<int>? employeeIds = null,
        int priority = 3,
        DateTime? startDate = null,
        DateTime? endDate = null) =>
        new()
        {
            Data = new CreateProjectRequest
            {
                Name = name,
                CustomerCompany = customerCompany,
                ExecutingCompany = executingCompany,
                ProjectManagerId = projectManagerId,
                EmployeeIds = employeeIds ?? new List<int>(),
                Priority = priority,
                StartDate = startDate ?? DefaultStart,
                EndDate = endDate ?? DefaultEnd
            }
        };

    [Fact]
    public async Task Handle_ValidDataWithoutParticipants_CreatesProjectAndReturnsId()
    {
        var pm = CreateEmployee(id: 1);
        _employeeRepo.GetEmployeeByIdAsync(1, Arg.Any<CancellationToken>()).Returns(pm);
        _employeeRepo.GetEmployeesByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee>());
        _projectRepo
            .When(x => x.AddProjectAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>()))
            .Do(x => x.Arg<Project>().GetType().GetProperty("Id")!.SetValue(x.Arg<Project>(), 99));

        var response = await _handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal(99, response.Id);
        await _projectRepo.Received(1).AddProjectAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _projectRepo.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProjectManagerNotFound_ThrowsEntityNotFoundException()
    {
        _employeeRepo.GetEmployeeByIdAsync(1, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(CreateCommand(), CancellationToken.None));

        await _projectRepo.DidNotReceive().AddProjectAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _projectRepo.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ParticipantNotFound_ThrowsEntityNotFoundException()
    {
        var pm = CreateEmployee(id: 1);
        _employeeRepo.GetEmployeeByIdAsync(1, Arg.Any<CancellationToken>()).Returns(pm);
        // Only 0 employees returned despite 1 being requested — missing participant
        _employeeRepo.GetEmployeesByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee>());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(CreateCommand(employeeIds: new List<int> { 2 }), CancellationToken.None));

        await _projectRepo.DidNotReceive().AddProjectAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PmIdIncludedInEmployeeIds_DroppedFromParticipantQuery()
    {
        var pm = CreateEmployee(id: 1);
        var participant = CreateEmployee(id: 2, email: "part@example.com");
        _employeeRepo.GetEmployeeByIdAsync(1, Arg.Any<CancellationToken>()).Returns(pm);
        _employeeRepo.GetEmployeesByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee> { participant });

        await _handler.Handle(CreateCommand(employeeIds: new List<int> { 1, 2 }), CancellationToken.None);

        await _employeeRepo.Received(1).GetEmployeesByIdsAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => !ids.Contains(1) && ids.Contains(2)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateParticipantIds_DeduplicatedBeforeQuery()
    {
        var pm = CreateEmployee(id: 1);
        var participant = CreateEmployee(id: 2, email: "part@example.com");
        _employeeRepo.GetEmployeeByIdAsync(1, Arg.Any<CancellationToken>()).Returns(pm);
        _employeeRepo.GetEmployeesByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee> { participant });

        await _handler.Handle(CreateCommand(employeeIds: new List<int> { 2, 2, 2 }), CancellationToken.None);

        await _employeeRepo.Received(1).GetEmployeesByIdsAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(2)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidData_CreatesProjectWithCorrectFields()
    {
        var pm = CreateEmployee(id: 1);
        _employeeRepo.GetEmployeeByIdAsync(1, Arg.Any<CancellationToken>()).Returns(pm);
        _employeeRepo.GetEmployeesByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee>());

        await _handler.Handle(CreateCommand(name: "My Project", priority: 5), CancellationToken.None);

        await _projectRepo.Received(1).AddProjectAsync(
            Arg.Is<Project>(p =>
                p.Name == "My Project" &&
                p.Priority == 5 &&
                p.ProjectManagerId == 1),
            Arg.Any<CancellationToken>());
    }
}
