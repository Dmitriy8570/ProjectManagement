using BusinessLogic.Employees;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _db;

    public EmployeeRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EmployeeDto>> SearchEmployeesAsync(
        string? term, int limit, IReadOnlyList<string>? roles, CancellationToken ct)
    {
        // Project to DTO with a left-join to AspNetUsers so the search hits a
        // single SQL round-trip. Left-join (not inner) so an employee in a
        // transient state without a linked account doesn't silently disappear
        // from the dropdown — the case shouldn't happen in practice but
        // surfacing a row with an empty email is better than swallowing it.
        var query =
            from e in _db.Employees.AsNoTracking()
            join u in _db.Users on e.Id equals u.EmployeeId into users
            from u in users.DefaultIfEmpty()
            select new
            {
                Employee = e,
                UserId = u != null ? u.Id : null,
                Email = u != null ? u.Email : null
            };

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
            query = query.Where(x =>
                EF.Functions.Like(x.Employee.FirstName, pattern) ||
                EF.Functions.Like(x.Employee.LastName, pattern) ||
                EF.Functions.Like(x.Employee.Patronymic, pattern) ||
                (x.Email != null && EF.Functions.Like(x.Email, pattern)));
        }

        if (roles is { Count: > 0 })
        {
            // Identity stores role names normalized (uppercased). Compare on
            // NormalizedName so the subquery matches whatever casing the caller
            // passed in.
            var normalizedRoles = roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.ToUpperInvariant())
                .ToArray();

            if (normalizedRoles.Length > 0)
            {
                // Subquery yielding every UserId that holds one of the requested
                // roles. EF translates this to a SQL EXISTS / IN — no client-side
                // materialization, single round-trip.
                var allowedUserIds =
                    from ur in _db.UserRoles
                    join r in _db.Roles on ur.RoleId equals r.Id
                    where r.NormalizedName != null && normalizedRoles.Contains(r.NormalizedName)
                    select ur.UserId;

                query = query.Where(x =>
                    x.UserId != null && allowedUserIds.Contains(x.UserId));
            }
        }

        return await query
            .OrderBy(x => x.Employee.LastName)
            .ThenBy(x => x.Employee.FirstName)
            .ThenBy(x => x.Employee.Id)
            .Take(limit)
            .Select(x => new EmployeeDto
            {
                Id = x.Employee.Id,
                FirstName = x.Employee.FirstName,
                LastName = x.Employee.LastName,
                Patronymic = x.Employee.Patronymic,
                Email = x.Email ?? string.Empty
            })
            .ToListAsync(ct);
    }

    public Task<EmployeeDto?> GetEmployeeDtoByIdAsync(int id, CancellationToken ct) =>
        (from e in _db.Employees.AsNoTracking()
         join u in _db.Users on e.Id equals u.EmployeeId into users
         from u in users.DefaultIfEmpty()
         where e.Id == id
         select new EmployeeDto
         {
             Id = e.Id,
             FirstName = e.FirstName,
             LastName = e.LastName,
             Patronymic = e.Patronymic,
             Email = u != null ? (u.Email ?? string.Empty) : string.Empty
         }).FirstOrDefaultAsync(ct);

    public Task<Employee?> GetEmployeeByIdAsync(int id, CancellationToken ct) =>
        _db.Employees
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Employee>> GetEmployeesByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        return await _db.Employees
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(ct);
    }

    public async Task AddEmployeeAsync(Employee employee, CancellationToken ct)
    {
        await _db.Employees.AddAsync(employee, ct);
    }

    public async Task<bool> DeleteEmployeeAsync(int id, CancellationToken ct)
    {
        var rowsAffected = await _db.Employees
            .Where(e => e.Id == id)
            .ExecuteDeleteAsync(ct);

        return rowsAffected > 0;
    }

    public Task<bool> IsProjectManagerAsync(int employeeId, CancellationToken ct) =>
        _db.Projects
            .AsNoTracking()
            .AnyAsync(p => p.ProjectManagerId == employeeId, ct);

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
