namespace BusinessLogic.Employees;

public interface IEmployeeRepository
{
    /// <summary>
    /// Searches employees by a free-text term (matched against first/last
    /// name, patronymic and e-mail, case-insensitive). When <paramref name="term"/>
    /// is null or whitespace, the repository returns the first
    /// <paramref name="limit"/> employees ordered by last/first name — this is
    /// what feeds the wizard's AJAX dropdown on initial open.
    ///
    /// When <paramref name="roles"/> is non-null/non-empty, only employees
    /// whose linked Identity account belongs to at least one of the listed
    /// roles are returned. The Email field on the returned DTOs is sourced
    /// from the linked Identity user account via a join.
    /// </summary>
    Task<IReadOnlyList<EmployeeDto>> SearchEmployeesAsync(
        string? term, int limit, IReadOnlyList<string>? roles, CancellationToken ct);

    /// <summary>
    /// Single-employee read for the detail page. Returns <c>null</c> when no
    /// employee exists with the given id. The DTO's Email comes from the
    /// linked Identity user account via a join.
    /// </summary>
    Task<EmployeeDto?> GetEmployeeDtoByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns the tracked entity for write-side flows (edit/delete). The
    /// entity itself does not carry Email — use <see cref="IBusinessLogic.Identity.IUserAccountService"/>
    /// when the email is needed alongside.
    /// </summary>
    Task<Employee?> GetEmployeeByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Loads multiple employees in a single round-trip. Order of the returned
    /// list is unspecified; the caller is expected to match by id. Missing
    /// ids are simply absent from the result — checking the count against
    /// the requested set is the standard way to detect bad input.
    /// </summary>
    Task<IReadOnlyList<Employee>> GetEmployeesByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task AddEmployeeAsync(Employee employee, CancellationToken ct);

    /// <summary>
    /// Removes the employee with the given id and returns whether a row was
    /// actually deleted (<c>false</c> means "not found"). Commits immediately
    /// at the SQL level — does not participate in the unit of work, so a
    /// follow-up <see cref="SaveAsync"/> call is not required.
    /// </summary>
    Task<bool> DeleteEmployeeAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns true if the employee is the project manager of at least one
    /// project. The PM foreign key is RESTRICT-on-delete at the database
    /// level, so callers use this to surface a friendly domain error before
    /// the SQL constraint fires.
    /// </summary>
    Task<bool> IsProjectManagerAsync(int employeeId, CancellationToken ct);

    /// <summary>
    /// Persists pending changes. EF-style: mutations on tracked entities are
    /// captured automatically, so there is no explicit Update method.
    /// </summary>
    Task SaveAsync(CancellationToken ct);
}
