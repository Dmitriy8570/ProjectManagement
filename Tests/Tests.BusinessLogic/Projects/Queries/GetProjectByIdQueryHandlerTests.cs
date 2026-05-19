using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Projects.Queries;

public class GetProjectByIdQueryHandlerTests
{
    private readonly IProjectRepository _repository;
    private readonly GetProjectByIdQueryHandler _handler;

    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd = new(2024, 12, 31);

    public GetProjectByIdQueryHandlerTests()
    {
        _repository = Substitute.For<IProjectRepository>();
        _handler = new GetProjectByIdQueryHandler(_repository);
    }

    private static Employee CreateEmployee(int id, string email = "person@example.com")
    {
        var employee = new Employee("First", "Last", "Patronymic", email);
        typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(employee, id);
        return employee;
    }

    private static Project CreateProject(int id, int pmId = 10, string name = "Name")
    {
        var pm = CreateEmployee(pmId, $"pm{pmId}@example.com");
        var project = new Project(name, "Customer", "Executing", DefaultStart, DefaultEnd, pm, 3);
        typeof(Project).GetProperty("Id")!.SetValue(project, id);
        return project;
    }

    [Fact]
    public async Task Handle_ProjectFound_ReturnsMappedDto()
    {
        var project = CreateProject(id: 3, pmId: 42, name: "My Project");
        _repository.GetProjectByIdAsync(3, Arg.Any<CancellationToken>()).Returns(project);
        var query = new GetProjectByIdQuery { Id = 3 };

        var dto = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(3, dto.Id);
        Assert.Equal("My Project", dto.Name);
        Assert.Equal(42, dto.ProjectManager.Id);
        Assert.Equal(DefaultStart, dto.StartDate);
        Assert.Equal(DefaultEnd, dto.EndDate);
        Assert.Equal(3, dto.Priority);
        Assert.Empty(dto.Employees);
    }

    [Fact]
    public async Task Handle_ProjectNotFound_ThrowsEntityNotFoundException()
    {
        _repository.GetProjectByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Project?)null);
        var query = new GetProjectByIdQuery { Id = 99 };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }
}
