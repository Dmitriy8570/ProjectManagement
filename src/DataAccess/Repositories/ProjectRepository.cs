using System.Linq.Expressions;
using BusinessLogic.Common;
using BusinessLogic.Projects;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _db;

    public ProjectRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Project>> GetProjectsAsync(ProjectListFilter filter, CancellationToken ct)
    {
        var query = _db.Projects.AsNoTracking();

        if (filter.StartDateFrom.HasValue)
            query = query.Where(p => p.StartDate >= filter.StartDateFrom.Value);
        if (filter.StartDateTo.HasValue)
            query = query.Where(p => p.StartDate <= filter.StartDateTo.Value);
        if (filter.EndDateFrom.HasValue)
            query = query.Where(p => p.EndDate >= filter.EndDateFrom.Value);
        if (filter.EndDateTo.HasValue)
            query = query.Where(p => p.EndDate <= filter.EndDateTo.Value);
        if (filter.MinPriority.HasValue)
            query = query.Where(p => p.Priority >= filter.MinPriority.Value);
        if (filter.MaxPriority.HasValue)
            query = query.Where(p => p.Priority <= filter.MaxPriority.Value);
        if (filter.ProjectManagerId.HasValue)
            query = query.Where(p => p.ProjectManagerId == filter.ProjectManagerId.Value);
        if (filter.ParticipantEmployeeId.HasValue)
            query = query.Where(p => p.Employees.Any(e => e.Id == filter.ParticipantEmployeeId.Value));
        if (!string.IsNullOrWhiteSpace(filter.NameSearch))
        {
            var term = filter.NameSearch.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term));
        }

        // Count over the filtered query, not the whole table — otherwise the
        // pager renders totals that don't match the visible rows.
        var totalCount = await query.CountAsync(ct);

        var ordered = filter.SortBy switch
        {
            ProjectSortBy.StartDate => Order(query, p => p.StartDate, filter.Descending),
            ProjectSortBy.EndDate   => Order(query, p => p.EndDate,   filter.Descending),
            ProjectSortBy.Priority  => Order(query, p => p.Priority,  filter.Descending),
            ProjectSortBy.Name      => Order(query, p => p.Name,      filter.Descending),
            _                       => query.OrderBy(p => p.Id)
        };

        // Tie-breaker by Id: rows with equal sort keys would otherwise be free
        // to shuffle between pages on subsequent requests.
        var items = await ordered
            .ThenBy(p => p.Id)
            .Include(p => p.ProjectManager)
            .Include(p => p.Employees)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Project>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        static IOrderedQueryable<Project> Order<TKey>(
            IQueryable<Project> q, Expression<Func<Project, TKey>> key, bool desc) =>
            desc ? q.OrderByDescending(key) : q.OrderBy(key);
    }

    public Task<Project?> GetProjectByIdAsync(int id, CancellationToken ct) =>
        _db.Projects
            .Include(p => p.ProjectManager)
            .Include(p => p.Employees)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddProjectAsync(Project project, CancellationToken ct) =>
        await _db.Projects.AddAsync(project, ct);

    public async Task<bool> DeleteProjectAsync(int id, CancellationToken ct)
    {
        var rowsAffected = await _db.Projects
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync(ct);

        return rowsAffected > 0;
    }

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}