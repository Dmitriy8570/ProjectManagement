using BusinessLogic.Employees;

namespace BusinessLogic.Projects;

public record ProjectDto
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string CustomerCompany { get; init; } = default!;
    public string ExecutingCompany { get; init; } = default!;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int Priority { get; init; }

    public EmployeeDto ProjectManager { get; init; } = default!;
    public IReadOnlyList<EmployeeDto> Employees { get; init; } = Array.Empty<EmployeeDto>();
}

internal static class ProjectMapping
{
    public static ProjectDto ToDto(this Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        CustomerCompany = project.CustomerCompany,
        ExecutingCompany = project.ExecutingCompany,
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        Priority = project.Priority,
        // ProjectManager is required on a valid Project, but the navigation
        // can still be null on a read path if Include() was skipped. Mapping
        // defensively avoids a NullReferenceException leaking up the stack.
        ProjectManager = project.ProjectManager?.ToDto() ?? new EmployeeDto
        {
            Id = project.ProjectManagerId
        },
        Employees = project.Employees.Select(e => e.ToDto()).ToArray()
    };
}
