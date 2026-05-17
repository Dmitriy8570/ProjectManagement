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
    public int Priority { get; private set; }

    public int ProjectManagerId { get; private set; }
    public Employee? ProjectManager { get; private set; }

    // Participants are stored in a private list and exposed as a read-only
    // view so callers must go through AddEmployee / RemoveEmployee — that
    // keeps the "PM is not also a participant" invariant enforceable.
    private readonly List<Employee> _employees = new();
    public IReadOnlyCollection<Employee> Employees => _employees;

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
    /// Idempotent: adding the same employee twice is a no-op. This keeps the
    /// API safe to call without pre-checking membership.
    /// Throws when the candidate is the current project manager — PM and the
    /// participants list are intentionally disjoint sets.
    /// </summary>
    public void AddEmployee(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        if (employee.Id == ProjectManagerId)
            throw new DomainValidationException(
                "The project manager cannot also be added as a regular participant.");

        if (_employees.Any(e => e.Id == employee.Id))
            return;

        _employees.Add(employee);
    }

    public bool RemoveEmployee(int employeeId)
    {
        var existing = _employees.FirstOrDefault(e => e.Id == employeeId);
        return existing is not null && _employees.Remove(existing);
    }

    /// <summary>
    /// Partial update: a <c>null</c> argument leaves the corresponding field
    /// unchanged. Dates are revalidated as a pair so the start/end invariant
    /// holds regardless of which side the caller changes. If the project
    /// manager changes and the new PM is currently a participant, they are
    /// silently removed from the participants list to preserve the
    /// "PM is not a participant" invariant.
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

            // Keep PM and Employees disjoint: if the new PM happened to be a
            // participant, drop them from the list rather than failing.
            var existing = _employees.FirstOrDefault(e => e.Id == projectManager.Id);
            if (existing is not null)
                _employees.Remove(existing);
        }

        if (priority.HasValue)
            Priority = DomainGuard.NonNegative(priority.Value, nameof(priority));
    }
}
