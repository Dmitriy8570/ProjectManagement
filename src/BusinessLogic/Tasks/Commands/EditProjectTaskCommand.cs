using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
using BusinessLogic.Employees;
using MediatR;

namespace BusinessLogic.Tasks.Commands;

/// <summary>
/// Partial update: only the fields the caller actually wants to change need
/// to be provided. Null values keep the existing data.
/// </summary>
public record EditProjectTaskRequest
{
    [MaxLength(200)]
    public string? Name { get; init; }

    [MaxLength(2000)]
    public string? Comment { get; init; }

    [Range(0, int.MaxValue)]
    public int? Priority { get; init; }

    public ProjectTaskStatus? Status { get; init; }

    [Range(0, int.MaxValue)]
    public int? AssigneeId { get; init; }
}

public record EditProjectTaskCommand : IRequest<EditProjectTaskResponse>
{
    public EditProjectTaskRequest Data { get; init; } = default!;
    public int Id { get; init; }
}

public record EditProjectTaskResponse
{
    public int Id { get; init; }
}

public class EditProjectTaskCommandHandler
    : IRequestHandler<EditProjectTaskCommand, EditProjectTaskResponse>
{
    private readonly IProjectTaskRepository _taskRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public EditProjectTaskCommandHandler(
        IProjectTaskRepository taskRepository,
        IEmployeeRepository employeeRepository)
    {
        _taskRepository = taskRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<EditProjectTaskResponse> Handle(
        EditProjectTaskCommand request, CancellationToken ct)
    {
        var data = request.Data;

        var task = await _taskRepository.GetTaskByIdAsync(request.Id, ct)
            ?? throw new EntityNotFoundException(nameof(ProjectTask), request.Id);

        task.Update(
            name: data.Name,
            comment: data.Comment,
            priority: data.Priority,
            status: data.Status);

        // Only resolve and re-assign if the caller wants to change the worker.
        if (data.AssigneeId.HasValue && data.AssigneeId.Value != task.AssigneeId)
        {
            var assignee = await _employeeRepository.GetEmployeeByIdAsync(data.AssigneeId.Value, ct)
                ?? throw new EntityNotFoundException(nameof(Employee), data.AssigneeId.Value);

            task.AssignWorker(assignee);
        }

        await _taskRepository.SaveAsync(ct);
        return new EditProjectTaskResponse { Id = task.Id };
    }
}
