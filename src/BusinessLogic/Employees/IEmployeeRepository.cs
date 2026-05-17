namespace BusinessLogic.Employees;

public interface IEmployeeRepository
{
    /// <summary>
    /// Searches employees by a free-text term (matched against first/last
    /// name, patronymic and e-mail, case-insensitive). When <paramref name="term"/>
    /// is null or whitespace, the repository returns the first
    /// <paramref name="limit"/> employees ordered by last/first name — this is
    /// what feeds the wizard's AJAX dropdown on initial open.
    /// </summary>
    Task<IReadOnlyList<Employee>> SearchEmployeesAsync(string? term, int limit, CancellationToken ct);

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
    /// Returns true if any employee already uses <paramref name="email"/>
    /// (case-insensitive), optionally excluding a known id (so that editing
    /// an employee without changing their email does not flag a conflict).
    /// </summary>
    Task<bool> EmailExistsAsync(string email, int? excludingId, CancellationToken ct);

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
