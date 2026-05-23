using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using BusinessLogic.Tasks;
using BusinessLogic.Tasks.Queries;
using NSubstitute;

namespace Tests.BusinessLogic.Tasks.Queries;

public class GetProjectTasksQueryHandlerTests
{
    private readonly IProjectTaskRepository _taskRepo;
    private readonly GetProjectTasksQueryHandler _handler;

    public GetProjectTasksQueryHandlerTests()
    {
        _taskRepo = Substitute.For<IProjectTaskRepository>();
        _handler = new GetProjectTasksQueryHandler(_taskRepo);
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

    private static ProjectTask CreateTask(int id, Project project, Employee author, Employee assignee, string name = "Task")
    {
        var task = new ProjectTask(project, author, assignee, name, null, 1);
        typeof(ProjectTask).GetProperty(nameof(ProjectTask.Id))!.SetValue(task, id);
        return task;
    }

    [Fact]
    public async Task Handle_ReturnsMappedDtos()
    {
        var pm = CreateEmployee(1);
        var project = CreateProject(10, pm);
        var tasks = new List<ProjectTask>
        {
            CreateTask(1, project, pm, pm, "Task A"),
            CreateTask(2, project, pm, pm, "Task B")
        };

        _taskRepo.GetTasksAsync(Arg.Any<ProjectTaskListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProjectTask>
            {
                Items = tasks,
                TotalCount = 2,
                Page = 1,
                PageSize = 20
            });

        var result = await _handler.Handle(
            new GetProjectTasksQuery { Filter = new ProjectTaskListFilter() },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Task A", result.Items[0].Name);
        Assert.Equal("Task B", result.Items[1].Name);
    }

    [Fact]
    public async Task Handle_ClampsPageToMinimumOne()
    {
        _taskRepo.GetTasksAsync(Arg.Any<ProjectTaskListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProjectTask>
            {
                Items = new List<ProjectTask>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        await _handler.Handle(
            new GetProjectTasksQuery { Filter = new ProjectTaskListFilter { Page = 0 } },
            CancellationToken.None);

        await _taskRepo.Received(1).GetTasksAsync(
            Arg.Is<ProjectTaskListFilter>(f => f.Page == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClampsPageSizeToMaxHundred()
    {
        _taskRepo.GetTasksAsync(Arg.Any<ProjectTaskListFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProjectTask>
            {
                Items = new List<ProjectTask>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 100
            });

        await _handler.Handle(
            new GetProjectTasksQuery { Filter = new ProjectTaskListFilter { PageSize = 500 } },
            CancellationToken.None);

        await _taskRepo.Received(1).GetTasksAsync(
            Arg.Is<ProjectTaskListFilter>(f => f.PageSize == 100),
            Arg.Any<CancellationToken>());
    }
}
