namespace BusinessLogic.Employees;

public interface IEmployeeRepository
{
    /// <summary>
    /// Searches employees by a free-text term (matched against first/last
    /// name and email). When <paramref name="term"/> is null or empty, the
    /// repository returns the first <paramref name="limit"/> employees —
    /// this is what feeds the wizard's AJAX dropdown on initial open.
    /// </summary>
    Task<IReadOnlyList<Employee>> SearchEmployeesAsync(string? term, int limit, CancellationToken ct);

    Task<Employee?> GetEmployeeByIdAsync(int id, CancellationToken ct);

    Task AddEmployeeAsync(Employee employee, CancellationToken ct);

    Task DeleteEmployeeAsync(int id, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
