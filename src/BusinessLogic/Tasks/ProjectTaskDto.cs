using BusinessLogic.Employees;

namespace BusinessLogic.Tasks;

public record ProjectTaskDto
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string Comment { get; init; } = default!;
    public ProjectTaskStatus Status { get; init; }
    public int Priority { get; init; }

    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = default!;

    public EmployeeDto Author { get; init; } = default!;
    public EmployeeDto Assignee { get; init; } = default!;
}

internal static class ProjectTaskMapping
{
    public static ProjectTaskDto ToDto(this ProjectTask task) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Comment = task.Comment,
        Status = task.Status,
        Priority = task.Priority,
        ProjectId = task.ProjectId,
        // Project name is shown on task lists outside a project context; falling
        // back to an empty string keeps the DTO valid if Include() was skipped.
        ProjectName = task.Project?.Name ?? string.Empty,
        Author = task.Author?.ToDto() ?? new EmployeeDto { Id = task.AuthorId },
        Assignee = task.Assignee?.ToDto() ?? new EmployeeDto { Id = task.AssigneeId }
    };
}
