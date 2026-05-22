using BusinessLogic.Common;

namespace BusinessLogic.Tasks;

public interface IProjectTaskRepository
{
    /// <summary>
    /// Returns one page of tasks matching <paramref name="filter"/>, already
    /// sorted as requested, together with the total count of matches. The
    /// project, author and assignee navigations are expected to be hydrated.
    /// </summary>
    Task<PagedResult<ProjectTask>> GetTasksAsync(ProjectTaskListFilter filter, CancellationToken ct);

    /// <summary>
    /// Loads a single task. The aggregate's project (including its participants
    /// and PM) must be hydrated so domain invariants — like
    /// "assignee must be a project member" — can be re-checked on edits.
    /// </summary>
    Task<ProjectTask?> GetTaskByIdAsync(int id, CancellationToken ct);

    Task AddTaskAsync(ProjectTask task, CancellationToken ct);

    /// <summary>
    /// Removes the task with the given id and returns whether a row was
    /// actually deleted (<c>false</c> means "not found"). Commits immediately
    /// at the SQL level — does not participate in the unit of work.
    /// </summary>
    Task<bool> DeleteTaskAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns true if the employee authored or is assigned to at least one
    /// task. Used by the Employee delete flow to surface a friendly domain
    /// error before the FK constraint fires.
    /// </summary>
    Task<bool> IsReferencedByAnyTaskAsync(int employeeId, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
