using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Projects.Queries;

public class GetEmployeeProjectsQueryHandlerTests
{
    private readonly IProjectRepository _repository = Substitute.For<IProjectRepository>();
    private readonly GetEmployeeProjectsQueryHandler _handler;

    public GetEmployeeProjectsQueryHandlerTests()
    {
        _handler = new GetEmployeeProjectsQueryHandler(_repository);
    }

    private static Employee CreateEmployee(int id, string lastName)
    {
        var e = new Employee("First", lastName, "Patr");
        typeof(Employee).GetProperty("Id")!.SetValue(e, id);
        return e;
    }

    private static Project CreateProject(int id, string name, Employee pm)
    {
        var p = new Project(name,
            customerCompany: "C", executingCompany: "E",
            startDate: new DateTime(2026, 1, 1),
            endDate:   new DateTime(2026, 12, 31),
            projectManager: pm,
            priority: 1);
        typeof(Project).GetProperty("Id")!.SetValue(p, id);
        return p;
    }

    private static PagedResult<Project> PageOf(params Project[] items) =>
        new() { Items = items, TotalCount = items.Length, Page = 1, PageSize = 100 };

    // The handler issues two filtered queries (manager-of and participant-of)
    // and splits the results into the two sections of the DTO.
    [Fact]
    public async Task Handle_SplitsManagedAndParticipantProjectsIntoSections()
    {
        var pm   = CreateEmployee(1, "Manager");
        var user = CreateEmployee(2, "Worker");

        var managed = CreateProject(10, "Managed",     pm);
        var member  = CreateProject(20, "ParticipatesIn", pm);

        _repository.GetProjectsAsync(
            Arg.Is<ProjectListFilter>(f => f.ProjectManagerId == 2),
            Arg.Any<CancellationToken>()).Returns(PageOf(managed));

        _repository.GetProjectsAsync(
            Arg.Is<ProjectListFilter>(f => f.ParticipantEmployeeId == 2),
            Arg.Any<CancellationToken>()).Returns(PageOf(member));

        var dto = await _handler.Handle(
            new GetEmployeeProjectsQuery { EmployeeId = 2 }, CancellationToken.None);

        Assert.Equal(new[] { "Managed" },        dto.ManagedProjects.Select(p => p.Name));
        Assert.Equal(new[] { "ParticipatesIn" }, dto.ParticipantProjects.Select(p => p.Name));
    }

    // Each filter must target the same employee id — protects against a
    // mix-up where one of the two queries accidentally filtered by the wrong
    // role.
    [Fact]
    public async Task Handle_QueriesBothRolesWithSameEmployeeId()
    {
        _repository.GetProjectsAsync(Arg.Any<ProjectListFilter>(), Arg.Any<CancellationToken>())
                   .Returns(PageOf());

        await _handler.Handle(
            new GetEmployeeProjectsQuery { EmployeeId = 42 }, CancellationToken.None);

        await _repository.Received(1).GetProjectsAsync(
            Arg.Is<ProjectListFilter>(f => f.ProjectManagerId == 42),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).GetProjectsAsync(
            Arg.Is<ProjectListFilter>(f => f.ParticipantEmployeeId == 42),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoProjects_ReturnsEmptyDto()
    {
        _repository.GetProjectsAsync(Arg.Any<ProjectListFilter>(), Arg.Any<CancellationToken>())
                   .Returns(PageOf());

        var dto = await _handler.Handle(
            new GetEmployeeProjectsQuery { EmployeeId = 1 }, CancellationToken.None);

        Assert.Empty(dto.ManagedProjects);
        Assert.Empty(dto.ParticipantProjects);
    }
}
