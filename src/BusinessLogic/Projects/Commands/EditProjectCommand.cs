using System.ComponentModel.DataAnnotations;
using BusinessLogic.Common;
using BusinessLogic.Employees;
using MediatR;

namespace BusinessLogic.Projects.Commands;

/// <summary>
/// Partial update: only the fields the caller actually wants to change need
/// to be provided. Null values keep the existing data.
/// </summary>
public record EditProjectRequest
{
    [Required]
    public int Id { get; init; }

    [MaxLength(200)]
    public string? Name { get; init; }

    [MaxLength(200)]
    public string? CustomerCompany { get; init; }

    [MaxLength(200)]
    public string? ExecutingCompany { get; init; }

    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }

    public int? ProjectManagerId { get; init; }

    [Range(0, int.MaxValue)]
    public int? Priority { get; init; }
}

public record EditProjectCommand : IRequest<EditProjectResponse>
{
    public EditProjectRequest Data { get; init; } = default!;
}

public record EditProjectResponse
{
    public int Id { get; init; }
}

public class EditProjectCommandHandler : IRequestHandler<EditProjectCommand, EditProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public EditProjectCommandHandler(
        IProjectRepository projectRepository,
        IEmployeeRepository employeeRepository)
    {
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<EditProjectResponse> Handle(EditProjectCommand request, CancellationToken ct)
    {
        var data = request.Data;

        var project = await _projectRepository.GetProjectByIdAsync(data.Id, ct)
            ?? throw new EntityNotFoundException(nameof(Project), data.Id);

        // Only resolve the new PM if the caller actually wants to change them.
        Employee? newProjectManager = null;
        if (data.ProjectManagerId.HasValue && data.ProjectManagerId.Value != project.ProjectManagerId)
        {
            newProjectManager = await _employeeRepository.GetEmployeeByIdAsync(data.ProjectManagerId.Value, ct)
                ?? throw new EntityNotFoundException(nameof(Employee), data.ProjectManagerId.Value);
        }

        project.Update(
            name: data.Name,
            customerCompany: data.CustomerCompany,
            executingCompany: data.ExecutingCompany,
            startDate: data.StartDate,
            endDate: data.EndDate,
            projectManager: newProjectManager,
            priority: data.Priority);

        await _projectRepository.SaveAsync(ct);

        return new EditProjectResponse { Id = project.Id };
    }
}
