using System.Linq.Expressions;
using BusinessLogic.Common;
using BusinessLogic.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public sealed class ProjectTaskRepository : IProjectTaskRepository
{
    private readonly AppDbContext _db;

    public ProjectTaskRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ProjectTask>> GetTasksAsync(
        ProjectTaskListFilter filter, CancellationToken ct)
    {
        var query = _db.ProjectTasks.AsNoTracking();

        if (filter.ProjectId.HasValue)
            query = query.Where(t => t.ProjectId == filter.ProjectId.Value);
        if (filter.AssigneeId.HasValue)
            query = query.Where(t => t.AssigneeId == filter.AssigneeId.Value);
        if (filter.AuthorId.HasValue)
            query = query.Where(t => t.AuthorId == filter.AuthorId.Value);
        if (filter.Status.HasValue)
            query = query.Where(t => t.Status == filter.Status.Value);
        if (filter.MinPriority.HasValue)
            query = query.Where(t => t.Priority >= filter.MinPriority.Value);
        if (filter.MaxPriority.HasValue)
            query = query.Where(t => t.Priority <= filter.MaxPriority.Value);
        if (!string.IsNullOrWhiteSpace(filter.NameSearch))
        {
            var term = filter.NameSearch.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(term));
        }

        // Count over the filtered query so the pager's totals match the rows.
        var totalCount = await query.CountAsync(ct);

        var ordered = filter.SortBy switch
        {
            ProjectTaskSortBy.Name     => Order(query, t => t.Name,     filter.Descending),
            ProjectTaskSortBy.Status   => Order(query, t => t.Status,   filter.Descending),
            ProjectTaskSortBy.Priority => Order(query, t => t.Priority, filter.Descending),
            _                          => query.OrderBy(t => t.Id)
        };

        var items = await ordered
            .ThenBy(t => t.Id)
            .Include(t => t.Project)
            .Include(t => t.Author)
            .Include(t => t.Assignee)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<ProjectTask>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        static IOrderedQueryable<ProjectTask> Order<TKey>(
            IQueryable<ProjectTask> q, Expression<Func<ProjectTask, TKey>> key, bool desc) =>
            desc ? q.OrderByDescending(key) : q.OrderBy(key);
    }

    public Task<ProjectTask?> GetTaskByIdAsync(int id, CancellationToken ct) =>
        _db.ProjectTasks
            .Include(t => t.Author)
            .Include(t => t.Assignee)
            // The Project navigation (with its PM and participants) is needed
            // so ProjectTask.AssignWorker can re-validate membership on edits.
            .Include(t => t.Project!).ThenInclude(p => p.ProjectManager)
            .Include(t => t.Project!).ThenInclude(p => p.Employees)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddTaskAsync(ProjectTask task, CancellationToken ct) =>
        await _db.ProjectTasks.AddAsync(task, ct);

    public async Task<bool> DeleteTaskAsync(int id, CancellationToken ct)
    {
        var rowsAffected = await _db.ProjectTasks
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct);

        return rowsAffected > 0;
    }

    public Task<bool> IsReferencedByAnyTaskAsync(int employeeId, CancellationToken ct) =>
        _db.ProjectTasks
            .AsNoTracking()
            .AnyAsync(t => t.AuthorId == employeeId || t.AssigneeId == employeeId, ct);

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
