using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Projects.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Projects.Queries;

public class GetProjectsQueryHandlerTests
{
    private readonly IProjectRepository _repository;
    private readonly GetProjectsQueryHandler _handler;

    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd = new(2024, 12, 31);

    public GetProjectsQueryHandlerTests()
    {
        _repository = Substitute.For<IProjectRepository>();
        _handler = new GetProjectsQueryHandler(_repository);
    }

    private static Employee CreateEmployee(int id, string email = "person@example.com")
    {
        var employee = new Employee("First", "Last", "Patronymic", email);
        typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(employee, id);
        return employee;
    }

    private static Project CreateProject(int id, int pmId = 10)
    {
        var pm = CreateEmployee(pmId, $"pm{pmId}@example.com");
        var project = new Project("Name", "Customer", "Executing", DefaultStart, DefaultEnd, pm, 3);
        typeof(Project).GetProperty("Id")!.SetValue(project, id);
        return project;
    }

    [Fact]
    public async Task Handle_ReturnsPagedResultMappedToDto()
    {
        var project = CreateProject(id: 5);
        _repository.GetProjectsAsync(Arg.Any<ProjectListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project> { Items = new[] { project }, TotalCount = 1, Page = 1, PageSize = 20 });

        var result = await _handler.Handle(new GetProjectsQuery(), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(5, result.Items[0].Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_PageBelowMin_ClampsTo1()
    {
        _repository.GetProjectsAsync(Arg.Any<ProjectListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project> { Items = Array.Empty<Project>(), TotalCount = 0, Page = 1, PageSize = 20 });

        await _handler.Handle(new GetProjectsQuery { Filter = new ProjectListFilter { Page = 0 } }, CancellationToken.None);

        await _repository.Received(1).GetProjectsAsync(
            Arg.Is<ProjectListFilter>(f => f.Page == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PageSizeAboveMax_ClampsTo100()
    {
        _repository.GetProjectsAsync(Arg.Any<ProjectListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project> { Items = Array.Empty<Project>(), TotalCount = 0, Page = 1, PageSize = 100 });

        await _handler.Handle(new GetProjectsQuery { Filter = new ProjectListFilter { PageSize = 500 } }, CancellationToken.None);

        await _repository.Received(1).GetProjectsAsync(
            Arg.Is<ProjectListFilter>(f => f.PageSize == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PageSizeBelowMin_ClampsTo1()
    {
        _repository.GetProjectsAsync(Arg.Any<ProjectListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project> { Items = Array.Empty<Project>(), TotalCount = 0, Page = 1, PageSize = 1 });

        await _handler.Handle(new GetProjectsQuery { Filter = new ProjectListFilter { PageSize = 0 } }, CancellationToken.None);

        await _repository.Received(1).GetProjectsAsync(
            Arg.Is<ProjectListFilter>(f => f.PageSize == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidPageAndSize_PassesUnchanged()
    {
        _repository.GetProjectsAsync(Arg.Any<ProjectListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project> { Items = Array.Empty<Project>(), TotalCount = 0, Page = 2, PageSize = 50 });

        await _handler.Handle(
            new GetProjectsQuery { Filter = new ProjectListFilter { Page = 2, PageSize = 50 } },
            CancellationToken.None);

        await _repository.Received(1).GetProjectsAsync(
            Arg.Is<ProjectListFilter>(f => f.Page == 2 && f.PageSize == 50),
            Arg.Any<CancellationToken>());
    }
}
