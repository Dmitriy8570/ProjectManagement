using BusinessLogic.Common;
using BusinessLogic.Employees;

namespace BusinessLogic.Projects;

public class Project
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string CustomerCompany { get; private set; } = default!;
    public string ExecutingCompany { get; private set; } = default!;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }

    public int ProjectManagerId { get; private set; }
    public Employee? ProjectManager { get; private set; }

    public List<Employee> Employees { get; private set; } = new();
    public int Priority { get; private set; }

    // Required by EF Core to rehydrate the entity from the database.
    private Project() { }

    public Project(
        string name,
        string customerCompany,
        string executingCompany,
        DateTime startDate,
        DateTime endDate,
        Employee projectManager,
        int priority)
    {
        ArgumentNullException.ThrowIfNull(projectManager);

        Name = DomainGuard.NotBlank(name, nameof(name), maxLength: 200);
        CustomerCompany = DomainGuard.NotBlank(customerCompany, nameof(customerCompany), maxLength: 200);
        ExecutingCompany = DomainGuard.NotBlank(executingCompany, nameof(executingCompany), maxLength: 200);

        (StartDate, EndDate) = DomainGuard.DateRange(startDate, endDate, nameof(startDate), nameof(endDate));

        ProjectManager = projectManager;
        ProjectManagerId = projectManager.Id;

        Priority = DomainGuard.NonNegative(priority, nameof(priority));
    }

    /// <summary>
    /// Idempotent: adding the same employee twice is a no-op. This keeps
    /// the API safe to call without callers having to pre-check membership.
    /// </summary>
    public void AddEmployee(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        if (Employees.Any(e => e.Id == employee.Id))
            return;

        Employees.Add(employee);
    }

    public bool RemoveEmployee(int employeeId)
    {
        var existing = Employees.FirstOrDefault(e => e.Id == employeeId);
        return existing is not null && Employees.Remove(existing);
    }

    /// <summary>
    /// Partial update: a <c>null</c> argument leaves the corresponding field
    /// unchanged. Dates are revalidated as a pair so the start/end invariant
    /// holds regardless of which side the caller changes.
    /// </summary>
    public void Update(
        string? name = null,
        string? customerCompany = null,
        string? executingCompany = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Employee? projectManager = null,
        int? priority = null)
    {
        if (name is not null)
            Name = DomainGuard.NotBlank(name, nameof(name), maxLength: 200);

        if (customerCompany is not null)
            CustomerCompany = DomainGuard.NotBlank(customerCompany, nameof(customerCompany), maxLength: 200);

        if (executingCompany is not null)
            ExecutingCompany = DomainGuard.NotBlank(executingCompany, nameof(executingCompany), maxLength: 200);

        if (startDate.HasValue || endDate.HasValue)
        {
            var newStart = startDate ?? StartDate;
            var newEnd = endDate ?? EndDate;
            (StartDate, EndDate) = DomainGuard.DateRange(newStart, newEnd, nameof(startDate), nameof(endDate));
        }

        if (projectManager is not null)
        {
            ProjectManager = projectManager;
            ProjectManagerId = projectManager.Id;
        }

        if (priority.HasValue)
            Priority = DomainGuard.NonNegative(priority.Value, nameof(priority));
    }
}
