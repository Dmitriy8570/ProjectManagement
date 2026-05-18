using BusinessLogic.Employees;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _db;

    public EmployeeRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Employee>> SearchEmployeesAsync(string? term, int limit, CancellationToken ct)
    {
        var query = _db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            // EF.Functions.Like maps to a case-insensitive LIKE on SQL Server
            // (default CI collation) and on SQLite with NOCASE; using it
            // keeps the dropdown predictable across both supported providers.
            var escaped = term.Trim()
              .Replace("\\", "\\\\")
              .Replace("%", "\\%")
              .Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            query = query.Where(e =>
                EF.Functions.Like(e.FirstName, pattern) ||
                EF.Functions.Like(e.LastName, pattern) ||
                EF.Functions.Like(e.Patronymic, pattern) ||
                EF.Functions.Like(e.Email, pattern));
        }

        return await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ThenBy(e => e.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task<Employee?> GetEmployeeByIdAsync(int id, CancellationToken ct) =>
        _db.Employees
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Employee>> GetEmployeesByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return Array.Empty<Employee>();

        return await _db.Employees
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(ct);
    }

    public async Task AddEmployeeAsync(Employee employee, CancellationToken ct) =>
        await _db.Employees.AddAsync(employee, ct);

    public async Task<bool> DeleteEmployeeAsync(int id, CancellationToken ct)
    {
        var rowsAffected = await _db.Employees
            .Where(e => e.Id == id)
            .ExecuteDeleteAsync(ct);

        return rowsAffected > 0;
    }

    public Task<bool> EmailExistsAsync(string email, int? excludingId, CancellationToken ct)
    {
        // Compare on lowered values rather than via LIKE — emails happen to
        // be free of LIKE wildcards in practice, but defending against them
        // here is cheaper than auditing every caller.
        var normalized = email.Trim().ToLower();
        return _db.Employees
            .AsNoTracking()
            .AnyAsync(e =>
                e.Email.ToLower() == normalized &&
                (excludingId == null || e.Id != excludingId),
                ct);
    }

    public Task<bool> IsProjectManagerAsync(int employeeId, CancellationToken ct) =>
        _db.Projects
            .AsNoTracking()
            .AnyAsync(p => p.ProjectManagerId == employeeId, ct);

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
