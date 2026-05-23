using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using MediatR;

namespace BusinessLogic.Tasks.Commands;

public record CreateProjectTaskRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = default!;

    [MaxLength(2000)]
    public string? Comment { get; init; }

    [Range(0, int.MaxValue)]
    public int ProjectId { get; init; }

    [Range(0, int.MaxValue)]
    public int AuthorId { get; init; }

    [Range(0, int.MaxValue)]
    public int AssigneeId { get; init; }

    [Range(0, int.MaxValue)]
    public int Priority { get; init; }

    public ProjectTaskStatus Status { get; init; } = ProjectTaskStatus.ToDo;
}

public record CreateProjectTaskCommand : IRequest<CreateProjectTaskResponse>
{
    public CreateProjectTaskRequest Data { get; init; } = default!;
}

public record CreateProjectTaskResponse
{
    public int Id { get; init; }
}

public sealed class CreateProjectTaskCommandHandler
    : IRequestHandler<CreateProjectTaskCommand, CreateProjectTaskResponse>
{
    private readonly IProjectTaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public CreateProjectTaskCommandHandler(
        IProjectTaskRepository taskRepository,
        IProjectRepository projectRepository,
        IEmployeeRepository employeeRepository)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<CreateProjectTaskResponse> Handle(
        CreateProjectTaskCommand request, CancellationToken ct)
    {
        var data = request.Data;

        // Load the project aggregate (with PM and participants) so the domain
        // constructor can verify the assignee belongs to the project team.
        var project = await _projectRepository.GetProjectByIdAsync(data.ProjectId, ct)
            ?? throw new EntityNotFoundException(nameof(Project), data.ProjectId);

        // Author and assignee are resolved in a single round-trip; either may
        // be the same person (an employee can file a task for themselves).
        var employeeIds = new HashSet<int> { data.AuthorId, data.AssigneeId };
        var employees = await _employeeRepository.GetEmployeesByIdsAsync(employeeIds, ct);
        var byId = employees.ToDictionary(e => e.Id);

        if (!byId.TryGetValue(data.AuthorId, out var author))
            throw new EntityNotFoundException(nameof(Employee), data.AuthorId);

        if (!byId.TryGetValue(data.AssigneeId, out var assignee))
            throw new EntityNotFoundException(nameof(Employee), data.AssigneeId);

        var task = new ProjectTask(
            project,
            author,
            assignee,
            data.Name,
            data.Comment,
            data.Priority,
            data.Status);

        await _taskRepository.AddTaskAsync(task, ct);
        await _taskRepository.SaveAsync(ct);

        return new CreateProjectTaskResponse { Id = task.Id };
    }
}
